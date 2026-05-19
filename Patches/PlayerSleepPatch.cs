using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks successful sleep-cycles while drunk for the Spirits' Respite feat.
    // Player.SetSleeping fires on both sleep-start and wake; we credit on wake
    // (sleep == false) since that's when the cycle has actually completed.
    // SE_Drunk runs in real time and Valheim sleep skips game-time only briefly,
    // so a drunk-asleep player typically still has SE_Drunk active on wake.
    // Not bed-specific — works for any future sleep method the game might add.
    [HarmonyPatch(typeof(Player), nameof(Player.SetSleeping))]
    public static class PlayerSleepPatch
    {
        public static void Postfix(Player __instance, bool sleep)
        {
            if (sleep) return; // we want wake-up, not bed-entry
            if (__instance != Player.m_localPlayer) return;
            if (!FeatTracker.IsDrunk(__instance)) return;

            FeatTracker.RecordEvent(__instance, Feats.DrunkSleeps);
        }
    }
}
