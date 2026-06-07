using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Drives the shared movement sampler once per frame for the local player.
    // Player.Update is private; Harmony patches it by name. All the gating and
    // accumulation lives in MovementTracker.
    [HarmonyPatch(typeof(Player), "Update")]
    public static class PlayerMovementPatch
    {
        private static Player _last;

        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            // New local player (respawn / world swap) - drop stale deltas/caches.
            if (_last != __instance)
            {
                _last = __instance;
                MovementTracker.Reset();
            }

            MovementTracker.Sample(__instance);
        }
    }
}
