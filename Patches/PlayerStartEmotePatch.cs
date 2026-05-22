using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Updates LocalEmoteTracker.CurrentEmote when the local player starts
    // an emote. Used by BoonSystem to detect the kneel ritual.
    //
    // Postfix (not prefix) so we record the emote even if a prefix in
    // another mod skips the vanilla method.
    [HarmonyPatch(typeof(Player), nameof(Player.StartEmote))]
    public static class PlayerStartEmotePatch
    {
        public static void Postfix(Player __instance, string emote)
        {
            if (__instance != Player.m_localPlayer) return;
            LocalEmoteTracker.CurrentEmote = emote ?? "";
        }
    }
}
