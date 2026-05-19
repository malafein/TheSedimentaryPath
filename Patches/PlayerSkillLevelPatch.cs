using HarmonyLib;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Records Rockery / Vinery skill-level high-water marks for the
    // "Grows Familiar" feats. Player.RaiseSkill is called frequently but
    // RecordPersonalBest is a cheap no-op unless the level actually advanced.
    [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
    public static class PlayerSkillLevelPatch
    {
        public static void Postfix(Player __instance, ValheimSkills.SkillType skill)
        {
            if (__instance != Player.m_localPlayer) return;

            int level = (int)__instance.GetSkillLevel(skill);
            if (level <= 0) return;

            if (skill == RockerySkill.SkillType)
                FeatTracker.RecordPersonalBest(__instance, Feats.RockerySkillLevel, level);
            else if (skill == VinerySkill.SkillType)
                FeatTracker.RecordPersonalBest(__instance, Feats.VinerySkillLevel, level);
        }
    }
}
