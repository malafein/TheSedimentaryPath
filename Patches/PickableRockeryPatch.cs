using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    /// <summary>
    /// Grants Rockery XP and a chance at a bonus drop when picking up
    /// stone-type pickables (Stone, Flint, Grausten, etc.).
    /// Runs independently of the Farming skill's vanilla bonus yield.
    /// </summary>
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    public static class PickableRockeryPatch
    {
        public static void Prefix(Pickable __instance, Humanoid character, ref int __state)
        {
            __state = 0;

            if (__instance.GetPicked()) return;

            Player player = character as Player;
            if (player == null) return;

            string pickableName = Utils.GetPrefabName(__instance.gameObject);
            float xp = RockerySkill.GetPickupXP(pickableName);
            if (xp <= 0f) return;

            player.RaiseSkill(RockerySkill.SkillType, xp);
            Log.Debug($"PickableRockeryPatch: raised Rockery by {xp} for picking {pickableName}");

            float skillFactor = player.GetSkillFactor(RockerySkill.SkillType);
            if (Random.value < skillFactor * RockerySkill.PickupBonusChance)
            {
                __state = RockerySkill.PickupBonusAmount;
                Log.Debug($"PickableRockeryPatch: bonus drop! +{__state} {pickableName} (skill={skillFactor:F2})");
            }
        }

        public static void Postfix(Pickable __instance, bool __result, int __state)
        {
            if (!__result || __state <= 0) return;

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
