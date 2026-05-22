using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Stone-Kin's specific ritual mechanic: kneel near a qualifying Rock
    // Shrine and hold the pose for a few seconds. Score must clear the
    // boon threshold (building score + elevation bonus). On completion,
    // calls BoonSystem.GrantBoon to apply the SE.
    //
    // Owns its own state machine; other boons get their own ritual class
    // with whatever conditions fit them.
    //
    // Flow:
    //   Idle      → kneeling near a qualifying shrine → Holding
    //   Holding   → held for RitualDurationSeconds   → Completed
    //               → kneel breaks or out of range   → Idle
    //   Completed → wait for kneel to end            → Idle
    //
    // Shrine search currently scans all ZNetViews — slow on large worlds
    // but only fires on kneel-start (rare). The proper
    // RockShrineComponent registry lands in E.3e and ScoreShrine /
    // MinBoonScore move to RockShrine at that point too.
    public static class StoneKinRitual
    {
        // ── Tuning ───────────────────────────────────────────────────────

        // Hold time for the ritual to complete.
        public const float RitualDurationSeconds = 5f;

        // Distance from the shrine required to start and maintain the
        // ritual.
        public const float ShrineProximityRadius = 6f;

        // Minimum total score (building + elevation) before the shrine
        // can grant any boon. TODO E.3e — move to RockShrine.
        public const int MinBoonScore = 30;

        // Max elevation contribution to shrine score, additive:
        //   elevationBonus = MaxElevationBonus × clamp01(y / WorldData.MaxMountainElevation)
        // TODO E.3e — move to RockShrine.
        public const float MaxElevationBonus = 180f;

        // ── State ────────────────────────────────────────────────────────

        private enum RitualState { Idle, Holding, Completed }

        private static RitualState _state = RitualState.Idle;
        private static float       _holdStartTime;
        private static Vector3     _activeShrinePos;
        private static int         _cachedScore;

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
        }

        // ── Score-with-elevation (TODO E.3e — move to RockShrine) ────────

        public static int ScoreShrine(GameObject shrineGo)
        {
            if (shrineGo == null) return 0;

            int buildingScore = RockShrine.ComputeScore(shrineGo.transform.position, shrineGo);

            float elevationBonus = 0f;
            if (WorldData.ScanComplete && WorldData.MaxMountainElevation > 0f)
            {
                float t = Mathf.Clamp01(shrineGo.transform.position.y / WorldData.MaxMountainElevation);
                elevationBonus = MaxElevationBonus * t;
            }

            return buildingScore + Mathf.RoundToInt(elevationBonus);
        }

        // ── Internals ────────────────────────────────────────────────────

        private static void TryStart(Player player)
        {
            GameObject shrineGo = FindNearestShrine(player.transform.position, ShrineProximityRadius);
            if (shrineGo == null) return;  // kneeling but not near a shrine — silent

            int score = ScoreShrine(shrineGo);
            if (score < MinBoonScore)
            {
                Notify.Center("this stone is not yet worthy", 0f);
                Log.Debug($"StoneKinRitual: rejected — score={score} below MinBoonScore={MinBoonScore}");
                _state = RitualState.Completed;  // hold here until kneel ends to avoid spamming
                return;
            }

            // Pre-check tier so we don't make the player hold the full
            // ritual duration just to find out they haven't earned it.
            int tier = BoonSystem.ComputeBoonTier(player, BoonRegistry.Get(TSPBoons.StoneKin));
            if (tier == 0)
            {
                Notify.Center("the stone does not yet know you as kin", 0f);
                Log.Debug("StoneKinRitual: rejected — boon tier 0 (feats not yet earned)");
                _state = RitualState.Completed;
                return;
            }

            _state           = RitualState.Holding;
            _holdStartTime   = Time.time;
            _activeShrinePos = shrineGo.transform.position;
            _cachedScore     = score;

            Log.Info($"StoneKinRitual: started — tier={tier} score={score} at {_activeShrinePos}");
        }

        private static void Complete(Player player)
        {
            // Recompute tier at completion in case feats advanced during
            // the hold (rare but possible). The pre-check at TryStart
            // protects against the common "boon not earned" case.
            BoonDef def = BoonRegistry.Get(TSPBoons.StoneKin);
            int tier = BoonSystem.ComputeBoonTier(player, def);

            if (tier > 0)
            {
                TSPBoons.ApplyStoneKin(player, tier, _cachedScore);
                Notify.Center("the stone takes you as kin", 0f);
            }

            Log.Info($"StoneKinRitual: completed — granted tier={tier} score={_cachedScore}");
            _state = RitualState.Completed;
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

        // Inline shrine search — temporary until E.3e introduces the
        // RockShrineComponent self-registering static list. Only fires on
        // kneel-start (a rare event), so cost is acceptable for now.
        private static GameObject FindNearestShrine(Vector3 playerPos, float radius)
        {
            GameObject best = null;
            float bestSqr = radius * radius;
            foreach (ZNetView znv in Object.FindObjectsOfType<ZNetView>())
            {
                if (!znv.IsValid()) continue;
                if (Utils.GetPrefabName(znv.gameObject) != RockShrine.RockPrefabName) continue;
                float sqr = (znv.transform.position - playerPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = znv.gameObject;
                }
            }
            return best;
        }
    }
}
