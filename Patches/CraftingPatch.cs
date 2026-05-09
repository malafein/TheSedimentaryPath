using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    public static class CraftingPatch
    {
        private static readonly FieldInfo CraftRecipeField =
            AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");

        public static void Postfix(InventoryGui __instance)
        {
            Recipe recipe = (Recipe)CraftRecipeField?.GetValue(__instance);
            if (recipe?.m_item == null)
                return;

            string itemName = recipe.m_item.m_itemData.m_shared.m_name;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            Skills.SkillType craftSkill;
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

            player.RaiseSkill(craftSkill, craftXP);
            Log.Debug($"CraftingPatch: raised {craftSkill} by {craftXP} for crafting {itemName}");

            // Bonus yield for stackable items
            if (recipe.m_item.m_itemData.m_shared.m_maxStackSize <= 1)
                return;

            float skillFactor = player.GetSkillFactor(craftSkill);
            float bonusChance = __instance.m_craftBonusChance;
            int bonusAmount = __instance.m_craftBonusAmount;

            if (Random.value < skillFactor * bonusChance)
            {
                Inventory inv = player.GetInventory();
                inv.AddItem(recipe.m_item.gameObject, bonusAmount);
                Log.Debug($"CraftingPatch: bonus craft! +{bonusAmount} {itemName} (skill={skillFactor:F2})");

                // Show bonus text above player
                DamageText.instance?.ShowText(DamageText.TextType.Bonus,
                    player.GetHeadPoint(), $"+{bonusAmount}", false);

                // Play bonus VFX if available
                if (__instance.m_craftBonusEffect != null)
                    __instance.m_craftBonusEffect.Create(player.transform.position, Quaternion.identity);
            }
        }
    }
}
