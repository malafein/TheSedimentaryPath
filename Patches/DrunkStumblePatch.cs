using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Injects a lateral Perlin sway into Player.SetControls's movedir so the
    // drunk stumble reads as intentional input rather than a post-velocity shove.
    // This triggers walk/run animation and rotates the character to face the
    // stumble direction, which ModifyWalkVelocity cannot do.
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    public static class DrunkStumblePatch
    {
        private const float SwaySpeed = 0.3f;    // match MeadCameraEffectPatch wobble
        private const float MaxSwayInput = 0.6f; // max lateral input magnitude at full drunk

        [HarmonyPrefix]
        public static void Prefix(Player __instance, ref Vector3 movedir)
        {
            if (__instance != Player.m_localPlayer) return;
            if (__instance.IsSitting() || __instance.IsAttached()) return; // Pause physical sway while sitting

            float multiplier = MeadCameraEffectPatch.GetDrunkMultiplier(__instance);
            if (multiplier <= 0f) return;

            float t = Time.time * SwaySpeed;
            float sway = (Mathf.PerlinNoise(t, 0.8f) - 0.5f) * 2f * MaxSwayInput * multiplier;

            movedir.x += sway;
            movedir.x = Mathf.Clamp(movedir.x, -1f, 1f);
        }
    }
}
