using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Stone-Kin's knockback reduction. Prefix scales the pushForce
    // parameter before vanilla's equipment-modifier / mass / clamp
    // math runs, so the multiplier composes naturally:
    //   final = (pushForce × boonScale) × Clamp01(1 + equipmentMod) / mass × 2.5
    //
    // The doctrine forbids body armor (helmet, chest, legs all empty),
    // so equipmentMod contributions can only come from utility-slot
    // items (cape, belt) and are typically small. Tier 3 alone gives
    // ~75% reduction — the apex of the discipline.
    //
    // Per-tier multipliers:
    //   Tier 1: × 0.7  (30% reduction)
    //   Tier 2: × 0.5  (50% reduction)
    //   Tier 3: × 0.25 (75% reduction)
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), typeof(Vector3), typeof(float))]
    public static class CharacterApplyPushbackPatch
    {
        private static readonly int StoneKinSEHash = "SE_StoneKin".GetStableHashCode();

        public static void Prefix(Character __instance, ref float pushForce)
        {
            if (!(__instance is Player player)) return;
            if (player != Player.m_localPlayer) return;
            if (pushForce <= 0f) return;

            SE_StoneKin se = player.GetSEMan()?.GetStatusEffect(StoneKinSEHash) as SE_StoneKin;
            if (se == null) return;
            if (!StoneKinPredicate.IsActive(player)) return;

            float multiplier;
            switch (se.Tier)
            {
                case 1:  multiplier = 0.7f;  break;
                case 2:  multiplier = 0.5f;  break;
                case 3:  multiplier = 0.25f; break;
                default: return;
            }

            pushForce *= multiplier;
        }
    }
}
