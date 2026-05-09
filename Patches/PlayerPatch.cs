using System.Reflection;
using HarmonyLib;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(Player), "Awake")]
    public static class PlayerAwakePatch
    {
        private static readonly FieldInfo SkillsField =
            AccessTools.Field(typeof(Player), "m_skills");

        public static void Postfix(Player __instance)
        {
            ValheimSkills skills = (ValheimSkills)SkillsField?.GetValue(__instance);
            if (skills == null)
            {
                Log.Warn("PlayerAwakePatch: m_skills is null");
                return;
            }

            RockerySkill.RegisterSkill(skills);
            VinerySkill.RegisterSkill(skills);

            // Attach MonoBehaviours to every player; they self-limit to local player only
            if (__instance.GetComponent<VineWatcher>() == null)
                __instance.gameObject.AddComponent<VineWatcher>();
            if (__instance.GetComponent<RockeryProximityDetector>() == null)
                __instance.gameObject.AddComponent<RockeryProximityDetector>();
            if (__instance.GetComponent<VineryProximityDetector>() == null)
                __instance.gameObject.AddComponent<VineryProximityDetector>();
        }
    }
}
