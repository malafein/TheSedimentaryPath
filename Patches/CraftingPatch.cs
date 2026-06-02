using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    public static class CraftingPatch
    {
        private static readonly FieldInfo CraftRecipeField =
            AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");
        private static readonly FieldInfo MultiCraftingField =
            AccessTools.Field(typeof(InventoryGui), "m_multiCrafting");
        private static readonly FieldInfo MultiCraftAmountField =
            AccessTools.Field(typeof(InventoryGui), "m_multiCraftAmount");

        public static void Postfix(InventoryGui __instance)
        {
            Recipe recipe = (Recipe)CraftRecipeField?.GetValue(__instance);
            if (recipe?.m_item == null)
                return;

            string itemName = recipe.m_item.m_itemData.m_shared.m_name;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            // A single DoCrafting call produces the whole "craft xN" batch, so
            // every per-item effect below scales by the multi-craft amount.
            bool multiCrafting = MultiCraftingField != null
                && (bool)MultiCraftingField.GetValue(__instance);
            int amount = multiCrafting && MultiCraftAmountField != null
                ? Mathf.Max(1, (int)MultiCraftAmountField.GetValue(__instance))
                : 1;

            // Journal: The Tilted Craft — any item crafted while drunk.
            if (FeatTracker.IsDrunk(player))
                FeatTracker.RecordEvent(player, Feats.DrunkCrafting, amount);

            // Journal: The Knapping — TSP stone weapon crafted (not the brew base).
            if (RockerySkill.IsRockeryWeapon(itemName))
                FeatTracker.RecordEvent(player, Feats.StoneWeaponsCrafted, amount);

            ValheimSkills.SkillType craftSkill;
            float craftXP;

            if (RockerySkill.IsRockeryItem(itemName))
            {
                craftSkill = RockerySkill.SkillType;
                craftXP    = RockerySkill.CraftXP;
            }
            else if (VinerySkill.IsVineryItem(itemName))
            {
                craftSkill = VinerySkill.SkillType;
                craftXP    = VinerySkill.CraftXP;
            }
            else
            {
                return;
            }

            player.RaiseSkill(craftSkill, craftXP * amount);
            Log.Debug($"CraftingPatch: raised {craftSkill} by {craftXP * amount} for crafting {amount}x {itemName}");

            // Bonus yield for stackable items
            if (recipe.m_item.m_itemData.m_shared.m_maxStackSize <= 1)
                return;

            float skillFactor = player.GetSkillFactor(craftSkill);
            float bonusChance = __instance.m_craftBonusChance;
            int bonusAmount = __instance.m_craftBonusAmount;

            // Roll the bonus once per item in the batch, mirroring vanilla's
            // own per-item bonus loop in DoCrafting.
            int totalBonus = 0;
            for (int i = 0; i < amount; i++)
                if (Random.value < skillFactor * bonusChance)
                    totalBonus += bonusAmount;

            if (totalBonus > 0)
            {
                Inventory inv = player.GetInventory();
                inv.AddItem(recipe.m_item.gameObject, totalBonus);
                Log.Debug($"CraftingPatch: bonus craft! +{totalBonus} {itemName} (skill={skillFactor:F2})");

                // Show bonus text above player
                DamageText.instance?.ShowText(DamageText.TextType.Bonus,
                    player.GetHeadPoint(), $"+{totalBonus}", false);

                // Play bonus VFX if available
                if (__instance.m_craftBonusEffect != null)
                    __instance.m_craftBonusEffect.Create(player.transform.position, Quaternion.identity);
            }
        }
    }
}
