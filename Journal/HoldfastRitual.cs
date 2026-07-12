using System.Collections.Generic;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Holdfast's ritual mechanic: sit vigil among a watched grove. The
    // cult's own posture — sitting near watched vines engages the normal
    // watching mechanic DURING the ritual, so performing the ritual feeds
    // the site's future worthiness.
    //
    // Site score: summed accumulated watch time (the vinery_credit each
    // vine carries in its ZDO) across all grown vines within GatherRadius —
    // deliberately larger than the 5 m watch radius: watching only ever
    // bonds a couple of plants at a time, but the SITE is the whole
    // spread-out grove. Any watched vine counts; the score is something the
    // player GREW — patience as the cult's currency.
    //
    // Variable hold → boon duration (vs Stone-Kin's fixed 5 s kneel): the
    // longer the vigil is held, the longer the boon lasts, up to the tier's
    // cap — the boon lasts as long as you were willing to wait. The vigil
    // IS watching: held time accrues only while the watch bond has a live
    // target — merely sitting near the grove earns nothing. The bond keys
    // on BODY facing (camera movement can't break it), so in practice it's
    // decided at sit-down and constant for the sit; the per-tick check just
    // also covers the target itself dying mid-vigil (picked / destroyed),
    // where VineWatcher drops the bond. Two cues: one when the ritual takes
    // (minimum vigil reached), one when the cap is reached so the player
    // knows they may rise. The boon is granted when the sit ends.
    //
    // The ritual answers only at the take-moment (0.3.3, user direction
    // 2026-07-10): unworthy/tier-0 attempts hold the full minimum vigil too,
    // and the rejection line fires at MinVigilSeconds — where "the vine
    // takes hold" would — so the pilgrim genuinely attempts the ritual
    // before being answered. Rising early gets no answer.
    //
    // Flow (WATCHING begins the ritual — merely sitting near a grove opens
    // nothing and says nothing; the rejection lines are part of the ritual
    // and only speak to someone who watched out the minimum vigil):
    //   Idle      → began watching near a watched grove → Holding
    //   Holding   → rose before MinVigilSeconds watched → Idle (no answer)
    //             → MinVigilSeconds watched, unworthy
    //               site or tier 0                      → rejection, Completed
    //             → rose after MinVigilSeconds watched  → grant, Completed
    //   Completed → wait for the sit to end             → Idle
    public static class HoldfastRitual
    {
        // ── Tuning ───────────────────────────────────────────────────────

        // Radius around the sitting player whose vines fund the site score.
        public const float GatherRadius = 20f;

        // Minimum vigil for the ritual to take at all — well past the ~10 s
        // watching kick-in (the ritual asks more than watching does), and
        // far enough that "the vine takes hold" doesn't crowd the watch
        // mechanic's own message (raised 15 → 25 after testing, 2026-07-10).
        public const float MinVigilSeconds = 25f;

        // Hold time at which the accrued duration reaches the tier cap.
        // The 60-second vigil exists — as devotion, not as tax.
        public const float FullCapHoldSeconds = 50f;

        // Worthiness threshold in WATCHED-VINE UNITS (see ComputeGroveScore:
        // 1.0 = one segment watched through a full growth). Raw watch credit
        // is denominated in seconds of the target's own m_growTime, so
        // absolute sums are meaningless across vine types — first test
        // (2026-07-10) scored a modest debug grove at 31,552 raw against a
        // 1,800 threshold. Six units ≈ that same modest grove (an 8-segment
        // wall, each segment watched through most of a growth, ≈7.9) just
        // clearing the bar — the definition of a worthy site, not
        // comfortably above it.
        public const float MinGroveScore = 6f;

        // Per-segment ceiling so one obsessively watched vine can't fund a
        // whole site — the SITE is the grove; breadth over fixation.
        public const float SegmentScoreCap = 2f;

        // ── State ────────────────────────────────────────────────────────

        private enum RitualState { Idle, Holding, Completed }

        private static RitualState _state = RitualState.Idle;
        private static bool  _wasWatching;
        private static float _heldSeconds;
        private static float _groveScore;
        private static bool  _tookHold;
        private static bool  _capNotified;

        private static readonly Collider[] _groveColliders = new Collider[256];
        private static readonly HashSet<ZNetView> _groveSeen = new HashSet<ZNetView>();
        private static int s_mask;

        // ── BoonSystem hooks ─────────────────────────────────────────────

        public static void Tick(Player player, float dt)
        {
            bool watching = World.VineWatcher.LocalPlayerIsWatching;

            // Fast path: nothing in flight and no watch bond.
            if (_state == RitualState.Idle && !watching && !_wasWatching)
                return;

            bool beganWatching = watching && !_wasWatching;
            _wasWatching = watching;

            bool sitting = player != null && player.IsSitting();

            switch (_state)
            {
                case RitualState.Idle:
                    // Attempt once per watch session (the grove scan is not
                    // per-frame cheap); a watch begun with no watched grove
                    // around at all stays silent until the player rises and
                    // watches anew.
                    if (beganWatching) TryStart(player);
                    break;

                case RitualState.Holding:
                    if (sitting) UpdateHold(player, dt, watching);
                    else         Finish(player);
                    break;

                case RitualState.Completed:
                    // Wait for the sit to fully end before going Idle, so a
                    // completed (or rejected) ritual can't re-trigger.
                    if (!sitting) _state = RitualState.Idle;
                    break;
            }
        }

        public static void ClearAll()
        {
            _state       = RitualState.Idle;
            _wasWatching = false;
            _heldSeconds = 0f;
            _groveScore  = 0f;
            _tookHold    = false;
            _capNotified = false;
            // The doctrine predicate's rooted-grace stamp is per-session
            // state of the same boon — drop it alongside the ritual's.
            HoldfastPredicate.ClearAll();
        }

        // ── Internals ────────────────────────────────────────────────────

        private static void TryStart(Player player)
        {
            float score = ComputeGroveScore(player.transform.position);
            if (score <= 0f) return; // watching, but no watched grove around — silent

            // Worthiness and tier are deliberately NOT answered here — the
            // pilgrim watches out the full minimum vigil and is answered at
            // MinVigilSeconds in UpdateHold, at the moment the ritual would
            // take. The grove score is cached from this scan (it's not
            // per-frame cheap); tier is computed fresh at the take.
            _state       = RitualState.Holding;
            _heldSeconds = 0f;
            _groveScore  = score;
            _tookHold    = false;
            _capNotified = false;

            Log.Debug($"HoldfastRitual: vigil started — groveScore={score:0.00}");
        }

        private static void UpdateHold(Player player, float dt, bool watching)
        {
            // The vigil is watching — no live watch bond, no accrual (the
            // bond can only drop mid-sit if the watched vine itself dies).
            if (!watching) return;

            _heldSeconds += dt;
            float held = _heldSeconds;

            if (!_tookHold && held >= MinVigilSeconds)
            {
                // The minimum vigil has been watched out — now the ritual
                // answers. Rejection lines fire here, at the take-moment,
                // not at watch-start.
                if (_groveScore < MinGroveScore)
                {
                    Notify.Center("this grove is not yet worthy of the vine", 0f);
                    Log.Debug($"HoldfastRitual: rejected — score={_groveScore:0.00} below MinGroveScore={MinGroveScore:0}");
                    _state = RitualState.Completed; // hold here until the sit ends
                    return;
                }

                int tier = BoonSystem.ComputeBoonTier(player, BoonRegistry.Get(TSPBoons.Holdfast));
                if (tier == 0)
                {
                    Notify.Center("the vine does not yet know you", 0f);
                    Log.Debug("HoldfastRitual: rejected — boon tier 0 (trials not yet endured)");
                    _state = RitualState.Completed;
                    return;
                }

                _tookHold = true;
                Notify.Center("the vine takes hold", 0f);
            }

            if (_tookHold && !_capNotified)
            {
                BoonDef def  = BoonRegistry.Get(TSPBoons.Holdfast);
                int     tier = BoonSystem.ComputeBoonTier(player, def);
                float   cap  = (tier >= 1 && tier <= def.DurationByTier.Length)
                    ? def.DurationByTier[tier - 1]
                    : 0f;
                if (cap > 0f
                    && BoonSystem.AccruedDuration(held, MinVigilSeconds, FullCapHoldSeconds, cap) >= cap)
                {
                    _capNotified = true;
                    Notify.Center("the vine holds all it can", 0f);
                }
            }
        }

        // The sit ended — grant if the vigil was watched long enough.
        private static void Finish(Player player)
        {
            float held = _heldSeconds;

            if (held < MinVigilSeconds)
            {
                Log.Debug($"HoldfastRitual: vigil broken at {held:0.0}s (< {MinVigilSeconds:0}s) — no grant");
                _state = RitualState.Idle;
                return;
            }

            // Recompute tier at completion in case feats advanced during
            // the vigil (rare but possible).
            BoonDef def  = BoonRegistry.Get(TSPBoons.Holdfast);
            int     tier = BoonSystem.ComputeBoonTier(player, def);

            if (tier > 0)
            {
                float cap = (tier <= def.DurationByTier.Length) ? def.DurationByTier[tier - 1] : 0f;
                float duration = BoonSystem.AccruedDuration(held, MinVigilSeconds, FullCapHoldSeconds, cap);
                TSPBoons.ApplyHoldfast(player, tier, _groveScore, duration);
                Notify.Center("the vine holds fast to you", 0f);
                Log.Debug($"HoldfastRitual: completed — tier={tier} held={held:0.0}s duration={duration:0}s groveScore={_groveScore:0}");
            }

            _state = RitualState.Completed;
        }

        // Grove score in WATCHED-VINE UNITS: each grown vine segment within
        // GatherRadius contributes credit ÷ its own m_growTime — how many
        // full growths' worth of watching it has received (capped at
        // SegmentScoreCap). Raw vinery_credit is denominated in seconds of
        // the target's growth time (each max-skill tick advances 1/30th of
        // a growth), so this normalization makes scores comparable across
        // vine types and the thresholds legible. The watch mechanic spreads
        // credit segment by segment, so the sum over segments IS the
        // grove's accumulated watching. Grown vines only — saplings are
        // Plants, not yet part of the grove.
        private static float ComputeGroveScore(Vector3 center)
        {
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small");

            _groveSeen.Clear();
            float score = 0f;
            int count = Physics.OverlapSphereNonAlloc(center, GatherRadius, _groveColliders, s_mask);
            for (int i = 0; i < count; i++)
            {
                if (_groveColliders[i] == null) continue;
                Vine vine = _groveColliders[i].GetComponentInParent<Vine>();
                if (vine == null) continue;

                ZNetView nview = vine.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !_groveSeen.Add(nview)) continue;

                float credit = nview.GetZDO().GetFloat(VinerySkill.ZdoCreditKey, 0f);
                if (credit <= 0f) continue;
                float growTime = Mathf.Max(vine.m_growTime, 1f);
                score += Mathf.Min(credit / growTime, SegmentScoreCap);
            }

            Log.Debug($"HoldfastRitual: grove scan — {_groveSeen.Count} vine segment(s), score={score:0.00} watched-vine units");
            return score;
        }
    }
}
