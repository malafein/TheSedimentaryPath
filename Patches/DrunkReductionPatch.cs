using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(Player))]
    public static class DrunkReductionPatch
    {
        [HarmonyPatch("ConsumeItem")]
        [HarmonyPostfix]
        public static void ConsumeItem_Postfix(Player __instance, Inventory inventory, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer) return;

            // Only reduce drunk timer if the item is an actual food (provides health, stamina, or eitr).
            // Meads and potions should not sober you up!
            if (item.m_shared.m_food <= 0f && item.m_shared.m_foodStamina <= 0f && item.m_shared.m_foodEitr <= 0f) return;

            var seDrunk = __instance.GetSEMan()?.GetStatusEffect("SE_Drunk".GetStableHashCode()) as SE_Drunk;
            if (seDrunk != null)
            {
                seDrunk.ReduceAllTimers(60f); // Flat 60 seconds reduction when eating
            }
        }

        [HarmonyPatch("SetSleeping")]
        [HarmonyPrefix]
        public static void SetSleeping_Prefix(Player __instance, bool sleep)
        {
            if (sleep || __instance != Player.m_localPlayer) return;

            var seman = __instance.GetSEMan();
            if (seman != null)
            {
                seman.RemoveStatusEffect("SE_Drunk".GetStableHashCode(), quiet: true);
            }
        }
    }
}
