using HarmonyLib;
using UnityEngine;

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

    // Ensures the brew's fall speed restoration runs after ALL status effects
    // have applied their walk velocity changes — order-independent.
    [HarmonyPatch(typeof(SEMan), "ModifyWalkVelocity")]
    public static class BlackstoneBrewFallSpeedPatch
    {
        private static readonly System.Reflection.FieldInfo CharacterField =
            AccessTools.Field(typeof(SEMan), "m_character");

        public static void Postfix(SEMan __instance, ref Vector3 vel)
        {
            if (vel.y >= 0f) return; // only during a fall

            var character = CharacterField?.GetValue(__instance) as Character;
            if (character == null || character.IsOnGround()) return;

            var se = character.GetSEMan()?.GetStatusEffect("SE_BlackstoneBrew".GetStableHashCode()) as SE_BlackstoneBrew;
            if (se == null) return;

            // Override any slow-fall cap — you fall at normal speed
            vel.y = Mathf.Min(vel.y, -20f);
        }
    }
}
