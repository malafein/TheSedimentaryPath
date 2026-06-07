using System.Reflection;
using HarmonyLib;
using UnityEngine;
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
        // Player.GetSkillLevel applies temporary skill-level modifiers (food/brew
        // buffs add to the level), so banking it as a personal best would cross a
        // feat tier on a transient buff. Read the raw base level off the Skill
        // instead — Player.m_skills + Skills.GetSkill are private, so reach them by
        // reflection (same approach DebugCommands uses).
        private static readonly FieldInfo _skillsField =
            AccessTools.Field(typeof(Player), "m_skills");
        private static readonly MethodInfo _getSkillMethod =
            AccessTools.Method(typeof(ValheimSkills), "GetSkill", new[] { typeof(ValheimSkills.SkillType) });

        public static void Postfix(Player __instance, ValheimSkills.SkillType skill)
        {
            if (__instance != Player.m_localPlayer) return;

            int level = GetBaseSkillLevel(__instance, skill);
            if (level <= 0) return;

            if (skill == RockerySkill.SkillType)
                FeatTracker.RecordPersonalBest(__instance, Feats.RockerySkillLevel, level);
            else if (skill == VinerySkill.SkillType)
                FeatTracker.RecordPersonalBest(__instance, Feats.VinerySkillLevel, level);

            LoreChecker.NotifySkill(__instance, skill);
        }

        // Floored base level (matching how the game floors GetSkillLevel), with no
        // SEMan modifiers applied. Falls back to the modified level if reflection
        // ever fails so the feat still tracks (just with the old behaviour).
        private static int GetBaseSkillLevel(Player player, ValheimSkills.SkillType skill)
        {
            var skills = _skillsField?.GetValue(player) as ValheimSkills;
            if (skills != null && _getSkillMethod != null)
            {
                if (_getSkillMethod.Invoke(skills, new object[] { skill }) is ValheimSkills.Skill s)
                    return (int)Mathf.Floor(s.m_level);
            }
            return (int)player.GetSkillLevel(skill);
        }
    }
}
