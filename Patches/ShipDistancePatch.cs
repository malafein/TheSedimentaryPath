using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Accumulates The Salt Path — cumulative horizontal distance the local
    // player has sailed on a boat. Hooks Ship.CustomFixedUpdate (physics
    // tick) and samples the ship's XZ position delta on whichever ship the
    // local player is currently aboard.
    //
    // Position-delta (not Ship.GetSpeed) so non-owner clients accumulate
    // correctly — transform positions are replicated; m_body velocity is
    // only reliable on the owner.
    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    public static class ShipDistancePatch
    {
        // Only one ship can hold the local player at a time — Ship.OnTriggerEnter
        // adds to a per-player s_currentShips list and IsPlayerInBoat is
        // exclusive. A single tracked-ship slot is sufficient.
        private static Ship _tracked;
        private static Vector3 _prevPos;
        private static float _meterAccumulator;

        // Discard any single-tick delta larger than this. Catches rare ship
        // teleports (edge-force kicks at the world boundary, owner handoff)
        // and the first sample after boarding.
        private const float TeleportThresholdM = 100f;

        public static void Postfix(Ship __instance)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !__instance.IsPlayerInBoat(player))
                return;

            Vector3 cur = __instance.transform.position;

            if (_tracked != __instance)
            {
                _tracked = __instance;
                _prevPos = cur;
                _meterAccumulator = 0f;
                return;
            }

            Vector3 d = cur - _prevPos;
            _prevPos = cur;

            float dist = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            if (dist > TeleportThresholdM) return;

            _meterAccumulator += dist;
            if (_meterAccumulator >= 1f)
            {
                int wholeMeters = (int)_meterAccumulator;
                _meterAccumulator -= wholeMeters;
                // quiet:true — sailing fires per-meter at ~10/sec. Tier
                // crossings still log at Info; only the per-increment Debug
                // line is suppressed.
                FeatTracker.RecordEvent(player, Feats.SeaDistanceSailed, wholeMeters, quiet: true);
            }
        }
    }
}
