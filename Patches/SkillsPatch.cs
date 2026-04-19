using HarmonyLib;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(Skills), "IsSkillValid")]
    public static class SkillsIsValidPatch
    {
        public static void Postfix(Skills.SkillType type, ref bool __result)
        {
            if (type == RockerySkill.SkillType || type == VinerySkill.SkillType)
                __result = true;
        }
    }
}
