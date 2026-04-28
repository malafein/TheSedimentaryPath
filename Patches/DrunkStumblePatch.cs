using System.Reflection;
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
            if (__instance.InEmote() || __instance.IsAttached()) return;

            float multiplier = MeadCameraEffectPatch.GetDrunkMultiplier(__instance);
            if (multiplier <= 0f) return;

            float t = Time.time * SwaySpeed;
            float sway = (Mathf.PerlinNoise(t, 0.8f) - 0.5f) * 2f * MaxSwayInput * multiplier;

            movedir.x += sway;
            movedir.x = Mathf.Clamp(movedir.x, -1f, 1f);
        }
    }

    // UpdateEmote (LateUpdate) calls StopEmote if InEmote() && m_moveDir != zero.
    // At high framerates, LateUpdate runs in frames where no FixedUpdate fired, so
    // m_moveDir can hold a stale non-zero value from drunk sway set in a prior physics
    // tick. Zeroing it here before the check fires prevents that stale value from
    // cancelling an emote the player just started. Intentional stand-up still works:
    // SetControls already called StopEmote and updated the ZDO when the player pressed
    // a movement key, so UpdateEmote clears the emote via the ZDO change regardless.
    [HarmonyPatch(typeof(Player), "UpdateEmote")]
    public static class DrunkUpdateEmotePatch
    {
        private static readonly FieldInfo MoveDirField =
            AccessTools.Field(typeof(Character), "m_moveDir");

        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (!__instance.InEmote()) return;

            float multiplier = MeadCameraEffectPatch.GetDrunkMultiplier(__instance);
            if (multiplier <= 0f) return;

            MoveDirField.SetValue(__instance, Vector3.zero);
        }
    }
}
