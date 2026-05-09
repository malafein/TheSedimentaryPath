using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class BlackstoneBrew
    {
        // Dark slate — starting point, tune after playtest.
        public static readonly Color Tint = new Color(0.30f, 0.32f, 0.38f);

        public static StatusEffect CreateStatusEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_BlackstoneBrew>();
            se.name = "SE_BlackstoneBrew";
            se.m_name = "$se_blackstonebrew";
            
            // Status Effect Configuration Data (The Template)
            se.m_ttl = SE_BlackstoneBrew.Duration;
            se.m_healthPerTick = 2f;
            se.m_tickInterval = 10f;
            se.m_swimStaminaUseModifier = 0.3f;
            se.m_skillLevel         = RockerySkill.SkillType;
            se.m_skillLevelModifier = 10f;
            se.m_tooltip = "$se_blackstonebrew_tooltip";
            se.m_startMessage = "$se_blackstonebrew_start";
            se.m_stopMessage = "$se_blackstonebrew_stop";
            se.m_startMessageType = MessageHud.MessageType.Center;
            se.m_stopMessageType = MessageHud.MessageType.Center;

            // Borrow icon from finished mead
            var meadPrefab = ObjectDB.instance?.GetItemPrefab("MeadHealthMinor");
            var icons = meadPrefab?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0)
                se.m_icon = VisualUtil.TintIcon(icons[0], Tint);
            else
                Log.Warn("BlackstoneBrew: MeadHealthMinor icon not found");

            return se;
        }

        // The unfermented base — crafted at the Cauldron.
        public static GameObject CreateBasePrefab()
        {
            var sourcePrefab = ObjectDB.instance?.GetItemPrefab("MeadBaseHealthMinor");
            if (sourcePrefab == null)
            {
                Log.Error("BlackstoneBrew.CreateBasePrefab: MeadBaseHealthMinor not found");
                return null;
            }

            var prefab = Object.Instantiate(sourcePrefab, Plugin.PrefabContainer);
            prefab.name = "BlackstoneBrewBase";

            var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_blackstonebrewbase";
            shared.m_description = "$item_blackstonebrewbase_desc";
            shared.m_consumeStatusEffect = null; // clear inherited mead SE — base goes in fermenter, not consumed directly
            shared.m_maxStackSize = 10;
            shared.m_weight = 0.3f;
            shared.m_icons = VisualUtil.TintIcons(shared.m_icons, Tint);
            VisualUtil.TintMaterials(prefab, Tint);

            Log.Debug("BlackstoneBrewBase: prefab created");
            return prefab;
        }

        // The fermented brew — produced by the Fermenter, consumed for the SE.
        public static GameObject CreateBrewPrefab(StatusEffect se)
        {
            var sourcePrefab = ObjectDB.instance?.GetItemPrefab("MeadHealthMinor");
            if (sourcePrefab == null)
            {
                Log.Error("BlackstoneBrew.CreateBrewPrefab: MeadHealthMinor not found");
                return null;
            }

            var prefab = Object.Instantiate(sourcePrefab, Plugin.PrefabContainer);
            prefab.name = "BlackstoneBrew";

            var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_blackstonebrew";
            shared.m_description = "$item_blackstonebrew_desc";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;

            // Buff is entirely in SE_BlackstoneBrew — no food slot used
            shared.m_food = 0f;
            shared.m_foodStamina = 0f;
            shared.m_foodEitr = 0f;
            shared.m_foodBurnTime = 0f;
            shared.m_foodRegen = 0f;

            shared.m_consumeStatusEffect = se;
            shared.m_maxStackSize = 10;
            shared.m_weight = 0.3f;
            shared.m_icons = VisualUtil.TintIcons(shared.m_icons, Tint);
            VisualUtil.TintMaterials(prefab, Tint);

            Log.Debug("BlackstoneBrew: prefab created");
            return prefab;
        }
    }
}
