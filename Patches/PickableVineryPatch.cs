using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    /// <summary>
    /// Grants a Vinery skill bonus yield when picking berries from a Vine
    /// (Vineberry, Ashvine, or any future vine-borne pickable).
    /// Runs independently of both the Farming skill's vanilla bonus yield
    /// and the Rockery bonus — all three can trigger on the same pick.
    /// </summary>
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    public static class PickableVineryPatch
    {
        public static void Prefix(Pickable __instance, Humanoid character, ref int __state)
        {
            __state = 0;

            if (__instance.GetPicked()) return;

            Player player = character as Player;
            if (player == null) return;

            // Only applies to berries growing on a Vine component
            if (__instance.GetComponentInParent<Vine>() == null) return;

            player.RaiseSkill(VinerySkill.SkillType, VinerySkill.PickupXP);
            Log.Debug($"PickableVineryPatch: raised Vinery by {VinerySkill.PickupXP} for picking Vineberry");

            float skillFactor = player.GetSkillFactor(VinerySkill.SkillType);
            if (Random.value < skillFactor * VinerySkill.PickupBonusChance)
            {
                __state = VinerySkill.PickupBonusAmount;
                Log.Debug($"PickableVineryPatch: bonus drop! +{__state} (Vinery skill={skillFactor:F2})");
            }
        }

        public static void Postfix(Pickable __instance, Humanoid character, bool __result, int __state)
        {
            if (!__result) return;

            // Journal: The Vine's Gift — count vine-borne pickups. Deliberately
            // combined: bindsinew shares the counter with vineberries.
            if (character is Player player && __instance.GetComponentInParent<Vine>() != null)
            {
                FeatTracker.RecordEvent(player, Feats.VineberriesHarvested);

                // Lore: the first Bindsinew drawn from a tended vine.
                if (__instance.m_itemPrefab != null
                    && __instance.m_itemPrefab.name == Items.TSPVineryWeapons.BindsinewPrefab)
                    JournalData.SetFlag(player, TSPLore.FirstBindsinew);
            }

            if (__state <= 0) return;

            GameObject itemPrefab = __instance.m_itemPrefab;
            if (itemPrefab == null) return;

            Vector3 pos = __instance.transform.position + Vector3.up * 0.3f;
            GameObject bonusDrop = Object.Instantiate(itemPrefab, pos, Quaternion.identity);
            ItemDrop itemDrop = bonusDrop.GetComponent<ItemDrop>();
            if (itemDrop != null)
                itemDrop.m_itemData.m_stack = __state;

            DamageText.instance?.ShowText(DamageText.TextType.Bonus, pos, $"+{__state}", false);
        }
    }
}
