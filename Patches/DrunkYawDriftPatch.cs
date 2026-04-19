using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Drifts Player.m_lookYaw with a Perlin signal synchronized to
    // DrunkStumblePatch's sway, so the camera gradually follows the direction
    // the character is being nudged. Side effect: holding W while drunk now
    // wanders organically instead of going straight.
    [HarmonyPatch(typeof(Player), "SetMouseLook")]
    public static class DrunkYawDriftPatch
    {
        private const float SwaySpeed = 0.3f;          // must match DrunkStumblePatch
        private const float DriftDegreesPerSecond = 30f; // at multiplier=1, perlin=±1

        private static readonly FieldInfo LookYawField =
            AccessTools.Field(typeof(Player), "m_lookYaw");

        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (LookYawField == null) return;

            float multiplier = MeadCameraEffectPatch.GetDrunkMultiplier(__instance);
            if (multiplier <= 0f) return;

            float t = Time.time * SwaySpeed;
            float perlin = (Mathf.PerlinNoise(t, 0.8f) - 0.5f) * 2f;
            float driftDegrees = perlin * DriftDegreesPerSecond * multiplier * Time.deltaTime;

            var currentYaw = (Quaternion)LookYawField.GetValue(__instance);
            LookYawField.SetValue(__instance, currentYaw * Quaternion.Euler(0f, driftDegrees, 0f));
        }
    }
}
