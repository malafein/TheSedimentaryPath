using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks player puke events for The Bitter Offering feat.
    // SE_Puke.Setup fires once per puke effect activation on the character.
    [HarmonyPatch(typeof(SE_Puke), nameof(SE_Puke.Setup))]
    public static class SE_PukePatch
    {
        public static void Postfix(Character character)
        {
            if (character is Player player && player == Player.m_localPlayer)
            {
                FeatTracker.RecordEvent(player, Feats.PukeCount);
            }
        }
    }
}
