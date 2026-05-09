using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class VineberryJuice
    {
        // Deep berry purple — starting point, tune after playtest.
        public static readonly Color Tint = new Color(0.60f, 0.30f, 0.75f);

        public static StatusEffect CreateStatusEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_VineberryJuice>();
            se.name = "SE_VineberryJuice";
            se.m_name = "$se_vineberryjuice";
            
            // Status Effect Configuration Data (The Template)
            se.m_ttl = SE_VineberryJuice.Duration;
            se.m_noiseModifier = 0.8f;
            se.m_runStaminaDrainModifier = 0.2f;
            se.m_skillLevel         = VinerySkill.SkillType;
            se.m_skillLevelModifier = 10f;
            se.m_tooltip = "$se_vineberryjuice_tooltip";
            se.m_startMessage = "$se_vineberryjuice_start";
            se.m_stopMessage = "$se_vineberryjuice_stop";
            se.m_startMessageType = MessageHud.MessageType.Center;
            se.m_stopMessageType = MessageHud.MessageType.Center;

            var meadPrefab = ObjectDB.instance?.GetItemPrefab("MeadStaminaMinor");
            var icons = meadPrefab?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0)
                se.m_icon = VisualUtil.TintIcon(icons[0], Tint);
            else
                Log.Warn("VineberryJuice: MeadStaminaMinor icon not found");

            return se;
        }

        public static GameObject CreateBasePrefab()
        {
            var sourcePrefab = ObjectDB.instance?.GetItemPrefab("MeadBaseStaminaMinor");
            if (sourcePrefab == null)
            {
                Log.Error("VineberryJuice.CreateBasePrefab: MeadBaseStaminaMinor not found");
                return null;
            }

            var prefab = Object.Instantiate(sourcePrefab, Plugin.PrefabContainer);
            prefab.name = "VineberryJuiceBase";

            var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_vineberryjuicebase";
            shared.m_description = "$item_vineberryjuicebase_desc";
            shared.m_consumeStatusEffect = null; // clear inherited mead SE — base goes in fermenter, not consumed directly
            shared.m_maxStackSize = 10;
            shared.m_weight = 0.3f;
            shared.m_icons = VisualUtil.TintIcons(shared.m_icons, Tint);
            VisualUtil.TintMaterials(prefab, Tint);

            Log.Debug("VineberryJuiceBase: prefab created");
            return prefab;
        }

        public static GameObject CreateJuicePrefab(StatusEffect se)
        {
            var sourcePrefab = ObjectDB.instance?.GetItemPrefab("MeadStaminaMinor");
            if (sourcePrefab == null)
            {
                Log.Error("VineberryJuice.CreateJuicePrefab: MeadStaminaMinor not found");
                return null;
            }

            var prefab = Object.Instantiate(sourcePrefab, Plugin.PrefabContainer);
            prefab.name = "VineberryJuice";

            var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_vineberryjuice";
            shared.m_description = "$item_vineberryjuice_desc";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;

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

            Log.Debug("VineberryJuice: prefab created");
            return prefab;
        }
    }
}
