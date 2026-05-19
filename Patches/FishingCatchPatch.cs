using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks successful fish catches while drunk for The Listing Cast feat.
    // FishingFloat.Catch is the canonical "fish reeled in" completion point —
    // fires only on successful catch, not on cast or hook.
    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
    public static class FishingCatchPatch
    {
        public static void Postfix(Character owner)
        {
            if (!(owner is Player player) || player != Player.m_localPlayer) return;
            if (!FeatTracker.IsDrunk(player)) return;

            FeatTracker.RecordEvent(player, Feats.FishCaughtDrunk);
        }
    }
}
