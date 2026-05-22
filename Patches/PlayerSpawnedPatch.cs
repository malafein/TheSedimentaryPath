using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Fires LoreChecker.EvaluateAll on every local-player spawn (initial
    // connect AND respawn after death) so any lore whose conditions are
    // already met but never event-dispatched gets caught up. Common cases:
    //
    //   - Save was opened with a TSP version that has lore entries not
    //     present in the previous version.
    //   - A condition's underlying state was satisfied by game code that
    //     doesn't go through a TSP Notify channel (e.g. a console grant).
    //   - Respawn after death — idempotent re-evaluation, cheap.
    //
    // EvaluateAll is O(entries × stages × condition-cost) and condition
    // costs are tiny dict reads / int compares, so no need to gate to
    // first-spawn-only.
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    public static class PlayerSpawnedPatch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            LoreChecker.EvaluateAll(__instance);
        }
    }
}
