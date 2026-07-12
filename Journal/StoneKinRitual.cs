using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Stone-Kin's specific ritual mechanic: kneel near a qualifying Rock
    // Shrine and hold the pose for a few seconds. Score must clear
    // RockShrine.MinBoonScore. On completion, calls BoonSystem.GrantBoon
    // to apply the SE.
    //
    // Owns its own state machine; other boons get their own ritual class
    // with whatever conditions fit them.
    //
    // The ritual answers only at the take-moment (0.3.3, user direction
    // 2026-07-10): unworthy/tier-0 attempts hold the full kneel too, and the
    // rejection line fires where the grant would — the pilgrim genuinely
    // attempts the ritual before being answered. Rising early gets no answer.
    //
    // Flow:
    //   Idle      → kneeling near a shrine (any score) → Holding
    //   Holding   → held for RitualDurationSeconds     → answered (grant, or
    //               rejection: unworthy place / tier 0) → Completed
    //             → kneel breaks or out of range        → Idle (no answer)
    //   Completed → wait for kneel to end               → Idle
    public static class StoneKinRitual
    {
        // ── Tuning ───────────────────────────────────────────────────────

        // Hold time for the ritual to complete.
        public const float RitualDurationSeconds = 5f;

        // Distance from the shrine required to start and maintain the
        // ritual.
        public const float ShrineProximityRadius = 6f;

        // ── State ────────────────────────────────────────────────────────

        private enum RitualState { Idle, Holding, Completed }

        private static RitualState _state = RitualState.Idle;
        private static float       _holdStartTime;
        private static Vector3     _activeShrinePos;
        private static int         _cachedScore;
        private static bool        _qualifying;

        // ── BoonSystem hooks ─────────────────────────────────────────────

        public static void Tick(Player player, float dt)
        {
            // Fast path: nothing in flight and not currently kneeling.
            if (_state == RitualState.Idle && LocalEmoteTracker.CurrentEmote.Length == 0)
                return;

            bool kneeling = LocalEmoteTracker.IsKneeling(player);

            switch (_state)
            {
                case RitualState.Idle:
                    if (kneeling) TryStart(player);
                    break;

                case RitualState.Holding:
                    if (!kneeling || !IsStillNearShrine(player))
                    {
                        Abort();
                        return;
                    }
                    if (Time.time - _holdStartTime >= RitualDurationSeconds)
                        Complete(player);
                    break;

                case RitualState.Completed:
                    // Wait for the player to end the emote before going Idle.
                    // Prevents immediate re-trigger if they keep kneeling.
                    if (!kneeling) _state = RitualState.Idle;
                    break;
            }
        }

        public static void ClearAll()
        {
            _state           = RitualState.Idle;
            _holdStartTime   = 0f;
            _activeShrinePos = Vector3.zero;
            _cachedScore     = 0;
            _qualifying      = false;
        }

        // ── Internals ────────────────────────────────────────────────────

        private static void TryStart(Player player)
        {
            RockShrineComponent shrine = RockShrine.FindNearest(player.transform.position, ShrineProximityRadius);
            if (shrine == null) return;  // kneeling but not near a shrine — silent

            // Worthiness and tier are deliberately NOT answered here — the
            // pilgrim holds the full kneel and is answered in Complete, at
            // the moment the ritual would take. Worthiness is cached at
            // kneel-start (the shrine can't change under a kneeling player);
            // tier is computed fresh at the take.
            _state           = RitualState.Holding;
            _holdStartTime   = Time.time;
            _activeShrinePos = shrine.transform.position;
            _cachedScore     = shrine.Score;
            _qualifying      = shrine.IsQualifyingForBoon;

            Log.Debug($"StoneKinRitual: kneel started — score={_cachedScore} qualifying={_qualifying} at {_activeShrinePos}");
        }

        // The kneel has been held — now the ritual answers. Rejection lines
        // fire here, at the take-moment, not at kneel-start.
        private static void Complete(Player player)
        {
            _state = RitualState.Completed;  // hold here until kneel ends to avoid spamming

            if (!_qualifying)
            {
                Notify.Center("this place is not yet worthy of the stone", 0f);
                Log.Debug($"StoneKinRitual: rejected — score={_cachedScore} below MinBoonScore={RockShrine.MinBoonScore}");
                return;
            }

            int tier = BoonSystem.ComputeBoonTier(player, BoonRegistry.Get(TSPBoons.StoneKin));
            if (tier == 0)
            {
                Notify.Center("the stone does not yet know you as kin", 0f);
                Log.Debug("StoneKinRitual: rejected — boon tier 0 (feats not yet earned)");
                return;
            }

            TSPBoons.ApplyStoneKin(player, tier, _cachedScore);
            Notify.Center("the stone takes you as kin", 0f);
            Log.Debug($"StoneKinRitual: completed — granted tier={tier} score={_cachedScore}");
        }

        private static void Abort()
        {
            Log.Debug("StoneKinRitual: aborted (kneel broken or moved out of range)");
            _state = RitualState.Idle;
        }

        private static bool IsStillNearShrine(Player player)
        {
            float sqrRadius = ShrineProximityRadius * ShrineProximityRadius;
            return (player.transform.position - _activeShrinePos).sqrMagnitude <= sqrRadius;
        }
    }
}
