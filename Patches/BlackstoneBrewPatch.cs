using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Injects the Blackstone Brew's decaying health/stamina bonus into the food
    // value calculation so the max health/stamina bars update naturally every
    // second alongside real food — without occupying a food slot.
    [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
    public static class BlackstoneBrewFoodPatch
    {
        public static void Postfix(Player __instance, ref float hp, ref float stamina)
        {
            var se = __instance.GetSEMan()?.GetStatusEffect("SE_BlackstoneBrew".GetStableHashCode()) as SE_BlackstoneBrew;
            if (se == null) return;

            float decay = se.GetDecayFactor();
            hp      += SE_BlackstoneBrew.HealthBonus  * decay;
            stamina += SE_BlackstoneBrew.StaminaBonus * decay;
        }
    }

    // Suppress Feather Cape's slow fall and fall damage protection when the Brew is active
    [HarmonyPatch(typeof(SE_Stats))]
    public static class BlackstoneBrewFallPatch
    {
        [HarmonyPatch("ModifyWalkVelocity")]
        [HarmonyPrefix]
        public static bool ModifyWalkVelocity_Prefix(SE_Stats __instance, ref Vector3 vel)
        {
            // Only care about effects that limit fall speed (like the Feather Cape)
            if (__instance.m_maxMaxFallSpeed > 0f && vel.y < 0f && __instance.name != "SE_BlackstoneBrew")
            {
                var seman = __instance.m_character?.GetSEMan();
                if (seman != null && seman.HaveStatusEffect("SE_BlackstoneBrew".GetStableHashCode()))
                {
                    // The brew suppresses the slow fall. We skip the original method so it doesn't clamp the fall speed.
                    return false; 
                }
            }
            return true;
        }

        [HarmonyPatch("ModifyFallDamage")]
        [HarmonyPrefix]
        public static bool ModifyFallDamage_Prefix(SE_Stats __instance, float baseDamage, ref float damage)
        {
            // Only care about effects that negate fall damage (like the Feather Cape with m_fallDamageModifier = -1)
            if (__instance.m_fallDamageModifier < 0f && __instance.name != "SE_BlackstoneBrew")
            {
                var seman = __instance.m_character?.GetSEMan();
                if (seman != null && seman.HaveStatusEffect("SE_BlackstoneBrew".GetStableHashCode()))
                {
                    // The brew suppresses the cape's fall damage protection.
                    return false; 
                }
            }
            return true;
        }
    }
}
