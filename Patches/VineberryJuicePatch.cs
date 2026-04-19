using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Decaying +35 stamina / +35 eitr bonus injected into the food value
    // calculation — same approach as BlackstoneBrew, but for stamina/eitr.
    [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
    public static class VineberryJuiceFoodPatch
    {
        public static void Postfix(Player __instance, ref float stamina, ref float eitr)
        {
            var se = __instance.GetSEMan()?.GetStatusEffect("SE_VineberryJuice".GetStableHashCode()) as SE_VineberryJuice;
            if (se == null) return;

            float decay = se.GetDecayFactor();
            stamina += SE_VineberryJuice.StaminaBonus * decay;
            eitr    += SE_VineberryJuice.EitrBonus    * decay;
        }
    }
}
