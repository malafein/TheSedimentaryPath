using HarmonyLib;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Applies a blended skill factor and split XP for weapons registered in
    // Plugin.SplitSkillWeapons (primary weapon skill + a secondary craft skill).
    // Designed for obsidian weapons (Rockery) and future Vinery weapons alike.
    //
    // Hot-path cost: one string equality check per GetSkillFactor / RaiseSkill call.
    // The dictionary lookup only runs when the equipped weapon changes.
    [HarmonyPatch(typeof(Player))]
    public static class SplitSkillPatch
    {
        // Weapon cache — refreshed only on weapon change, not every call.
        private static string _cachedWeaponName;
        private static bool _isSplitWeapon;
        private static ValheimSkills.SkillType _primarySkill;
        private static ValheimSkills.SkillType _secondarySkill;

        // Re-entry guards.
        private static bool _inSkillBlend;
        private static bool _raisingSecondary;

        private static void RefreshCache(Player player)
        {
            string name = player.GetCurrentWeapon()?.m_shared?.m_name;
            if (name == _cachedWeaponName) return;

            _cachedWeaponName = name;
            if (name != null && Plugin.SplitSkillWeapons.TryGetValue(name, out ValheimSkills.SkillType secondary))
            {
                _isSplitWeapon  = true;
                _secondarySkill = secondary;
                _primarySkill   = player.GetCurrentWeapon().m_shared.m_skillType;
            }
            else
            {
                _isSplitWeapon = false;
            }
        }

        // Blend the primary skill factor with the secondary skill factor (50/50).
        // Only fires when a split-skill weapon is equipped and the queried skill
        // matches the weapon's native skill type.
        [HarmonyPatch("GetSkillFactor")]
        [HarmonyPostfix]
        public static void GetSkillFactor_Postfix(Player __instance, ValheimSkills.SkillType skill, ref float __result)
        {
            if (__instance != Player.m_localPlayer || _inSkillBlend) return;
            RefreshCache(__instance);
            if (!_isSplitWeapon || skill != _primarySkill) return;

            _inSkillBlend = true;
            float secondaryFactor = __instance.GetSkillFactor(_secondarySkill);
            _inSkillBlend = false;

            __result = (__result + secondaryFactor) * 0.5f;
        }

        // Split XP evenly: halve the amount going to the primary skill and award
        // the other half to the secondary skill.
        [HarmonyPatch("RaiseSkill")]
        [HarmonyPrefix]
        public static void RaiseSkill_Prefix(Player __instance, ValheimSkills.SkillType skill, ref float value)
        {
            if (__instance != Player.m_localPlayer || _raisingSecondary) return;
            RefreshCache(__instance);
            if (!_isSplitWeapon || skill != _primarySkill) return;

            value *= 0.5f;

            _raisingSecondary = true;
            __instance.RaiseSkill(_secondarySkill, value);
            _raisingSecondary = false;
        }
    }
}
