using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks distinct traders interacted with for the Strangers Met feat.
    // Hooks Trader.Interact (opens the StoreGui). Keyed by Trader.m_name
    // ($npc_* token on Haldor/Hildir), falling back to the prefab name —
    // the BogWitch prefab ships with an EMPTY Trader.m_name, so she's
    // stored as "BogWitch".
    [HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
    public static class TraderPatch
    {
        public static void Postfix(Trader __instance, Humanoid character, bool hold)
        {
            if (hold) return;
            if (!(character is Player player) || player != Player.m_localPlayer) return;
            if (__instance == null) return;

            string entryId = __instance.m_name;
            if (string.IsNullOrEmpty(entryId))
                entryId = Utils.GetPrefabName(__instance.gameObject);
            if (string.IsNullOrEmpty(entryId)) return;

            FeatTracker.AddDistinct(player, Feats.TradersVisited, entryId);
        }
    }
}
