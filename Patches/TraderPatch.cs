using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks distinct traders interacted with for the Strangers Met feat.
    // Hooks Trader.Interact (opens the StoreGui). Keyed by Trader.m_name —
    // stable across vanilla traders (Haldor, Hildir, BogWitch) and any modded
    // traders that follow the same component pattern.
    [HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
    public static class TraderPatch
    {
        public static void Postfix(Trader __instance, Humanoid character, bool hold)
        {
            if (hold) return;
            if (!(character is Player player) || player != Player.m_localPlayer) return;
            if (__instance == null || string.IsNullOrEmpty(__instance.m_name)) return;

            FeatTracker.AddDistinct(player, Feats.TradersVisited, __instance.m_name);
        }
    }
}
