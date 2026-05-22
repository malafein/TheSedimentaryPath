using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Per-frame entry point for AchievementSystem.Tick. Gated on the local
    // player so the orchestrator runs exactly once per frame regardless of
    // how many Player instances exist (host + peers in the same scene).
    //
    // AchievementSystem.Tick is performance-critical and early-exits when
    // no state is active — keep this patch as thin as possible.
    [HarmonyPatch(typeof(Player), "Update")]
    public static class PlayerUpdatePatch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            LocalEmoteTracker.Tick(__instance);
            AchievementSystem.Tick(__instance, Time.deltaTime);
            BoonSystem.Tick(__instance, Time.deltaTime);
        }
    }
}
