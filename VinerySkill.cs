using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class VinerySkill
    {
        // Hash-based skill ID to avoid collisions with other mods.
        public static readonly int SkillId =
            (Plugin.ModGUID + ".vinery").GetStableHashCode() & 0x7FFFFFFF;

        public static readonly Skills.SkillType SkillType = (Skills.SkillType)SkillId;

        // XP awarded per RPC tick (every 10 seconds of active watching)
        public const float WatchXP = 0.5f;

        // XP awarded when successfully picking a pickable from a vine
        public const float PickupXP = 1.0f;

        // Credit applied per watch tick, scaled by watcher's skill factor (0-1).
        // Now represents a fraction of the target's total max growth time.
        // At max level, 30 ticks (5 minutes) covers 100% of max growth.
        public const float CreditPerTick = 1f / 30f;

        // Berry respawn: each credit second received advances the berry's picked-time
        // by this fraction of a real second. 1.0 = full 1:1 advancement.
        public const float MaxBerryRespawnBoost = 1.0f;

        // Bonus yield on vine berry pickup (same formula as Rockery)
        public const float PickupBonusChance = 0.25f;
        public const int PickupBonusAmount = 1;

        // Total seconds of watch time ever applied to this vine/plant.
        // Accumulates forever; used only for hover text display.
        public static readonly int ZdoCreditKey = "vinery_credit".GetStableHashCode();

        /// <summary>
        /// Returns true if this Plant sapling grows into a vine (i.e., its grown
        /// prefab has a Vine component).
        /// </summary>
        public static bool IsVinePlant(Plant plant)
        {
            if (plant == null) return false;
            foreach (GameObject prefab in plant.m_grownPrefabs)
            {
                if (prefab != null && prefab.GetComponent<Vine>() != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true for any Pickable that Vinery watching should benefit.
        /// Matches anything that raises the Farming skill on pick — berries, mushrooms,
        /// cultivated crops — while naturally excluding rocks and minerals.
        /// </summary>
        public static bool IsVineryWatchable(Pickable pickable) =>
            pickable != null && pickable.m_pickRaiseSkill == Skills.SkillType.Farming;

        public static void RegisterSkill(Skills skills)
        {
            foreach (Skills.SkillDef def in skills.m_skills)
            {
                if (def.m_skill == SkillType)
                {
                    ZLog.Log("[TheSedimentaryPath] VinerySkill: already registered");
                    return;
                }
            }

            Skills.SkillDef vinery = new Skills.SkillDef
            {
                m_skill = SkillType,
                m_increseStep = 1f,
                m_description = "$skill_vinery_desc"
            };

            // Use the Vineberry item icon if available, otherwise fall back to Dandelion
            GameObject iconSource = ZNetScene.instance?.GetPrefab("Pickable_Vineberry")
                                 ?? ZNetScene.instance?.GetPrefab("Dandelion");
            if (iconSource != null)
            {
                ItemDrop itemDrop = iconSource.GetComponentInChildren<ItemDrop>();
                if (itemDrop?.m_itemData?.m_shared?.m_icons != null && itemDrop.m_itemData.m_shared.m_icons.Length > 0)
                {
                    vinery.m_icon = itemDrop.m_itemData.m_shared.m_icons[0];
                    ZLog.Log($"[TheSedimentaryPath] VinerySkill: using icon from {iconSource.name}");
                }
            }

            skills.m_skills.Add(vinery);
            ZLog.Log("[TheSedimentaryPath] VinerySkill: registered Vinery skill");
        }
    }
}
