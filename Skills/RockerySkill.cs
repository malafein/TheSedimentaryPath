using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Skills
{
    public static class RockerySkill
    {
        // Derive skill ID from a stable hash of our mod GUID + skill name to avoid
        // collisions with other mods. Mask to positive int and ensure it's above the
        // vanilla skill range (0-999).
        public static readonly int SkillId =
            (Plugin.ModGUID + ".rockery").GetStableHashCode() & 0x7FFFFFFF;

        public static readonly ValheimSkills.SkillType SkillType = (ValheimSkills.SkillType)SkillId;

        // XP factors for pickables
        public const float StonePickupXP = 0.5f;
        public const float StoneRockPickupXP = 23.0f;

        // Bonus drop settings (matching vanilla Pickable defaults)
        public const float PickupBonusChance = 0.25f;
        public const int PickupBonusAmount = 1;

        // Crafting XP per craft action
        public const float CraftXP = 1f;

        // Rock-skipping XP and damage per successful water skip
        public const float SkipXPPerSkip     = 0.5f;
        public const float SkipDamagePerSkip = 1f;

        // Pickable prefab names that grant small XP
        private static readonly HashSet<string> SmallXPPickables = new HashSet<string>
        {
            "Pickable_Stone",
            "Pickable_Flint",
            "Pickable_Ashstone",
            "Pickable_SulfurRock",
            "Pickable_HardRockOffspring"
        };

        // Pickable prefab names that grant large XP
        private static readonly HashSet<string> LargeXPPickables = new HashSet<string>
        {
            "Pickable_StoneRock"
        };

        public static float GetPickupXP(string pickablePrefabName)
        {
            if (LargeXPPickables.Contains(pickablePrefabName))
                return StoneRockPickupXP;
            if (SmallXPPickables.Contains(pickablePrefabName))
                return StonePickupXP;
            return 0f;
        }

        // All Rockery-skill craftable items (drives Rockery XP on craft).
        public static bool IsRockeryItem(string itemName)
        {
            return itemName == "$item_heftystone"
                || itemName == "$item_smoothstone"
                || itemName == "$item_kaldmork"
                || itemName == "$item_dokkblad"
                || itemName == "$item_blackstonebrewbase";
        }

        // True for TSP stone-family weapons only (excludes the brew base).
        // Used by feat tracking and kill-attribution checks.
        public static bool IsRockeryWeapon(string itemName)
        {
            return itemName == "$item_heftystone"
                || itemName == "$item_smoothstone"
                || itemName == "$item_kaldmork"
                || itemName == "$item_dokkblad";
        }

        public static void RegisterSkill(ValheimSkills skills)
        {
            // Check if already registered
            foreach (ValheimSkills.SkillDef def in skills.m_skills)
            {
                if (def.m_skill == SkillType)
                {
                    Log.Debug("RockerySkill: already registered");
                    return;
                }
            }

            ValheimSkills.SkillDef rockery = new ValheimSkills.SkillDef
            {
                m_skill = SkillType,
                m_increseStep = 1f,
                m_description = "$skill_rockery_desc"
            };

            // Use the Stone item icon for the skill
            GameObject stonePrefab = ZNetScene.instance?.GetPrefab("Stone");
            if (stonePrefab != null)
            {
                ItemDrop itemDrop = stonePrefab.GetComponent<ItemDrop>();
                if (itemDrop?.m_itemData?.m_shared?.m_icons != null && itemDrop.m_itemData.m_shared.m_icons.Length > 0)
                {
                    rockery.m_icon = itemDrop.m_itemData.m_shared.m_icons[0];
                    Log.Debug("RockerySkill: using Stone icon");
                }
            }

            skills.m_skills.Add(rockery);
            Log.Info("RockerySkill: registered");
        }
    }
}
