using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Builds the in-game journal panel once the HUD is up. Parented to
    // Hud.m_rootObject.transform so the panel renders in the HUD overlay
    // layer (above world, alongside the rest of the HUD). The vanilla
    // Hud.SetVisible moves m_rootObject by localPosition rather than
    // SetActive, so our panel stays around correctly when the HUD hides.
    [HarmonyPatch(typeof(Hud), "Awake")]
    public static class HudAwakePatch
    {
        public static void Postfix(Hud __instance)
        {
            if (__instance == null || __instance.m_rootObject == null) return;
            JournalUIController.Build(__instance.m_rootObject.transform);
        }
    }
}
