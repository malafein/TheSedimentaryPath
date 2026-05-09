using HarmonyLib;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(ValheimSkills), "IsSkillValid")]
    public static class SkillsIsValidPatch
    {
        public static void Postfix(ValheimSkills.SkillType type, ref bool __result)
        {
            if (type == RockerySkill.SkillType || type == VinerySkill.SkillType)
                __result = true;
        }
    }
}
