using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Camera wobble + chromatic aberration driven by Perlin noise.
    // Postfixes UpdateCamera so our rotation applies after GameCamera has
    // already set its final transform for the frame.
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    public static class MeadCameraEffectPatch
    {
        // Wobble — tunable constants
        private const float BaseWobbleAmplitude = 12f;   // degrees; significantly increased
        private const float WobbleSpeed         = 0.3f;  // lower = slower sway

        // Chromatic aberration pulse
        private const float CAMin   = 0.5f;
        private const float CAMax   = 1.0f;
        private const float CASpeed = 0.4f;

        private const float DrunkDuration = 900f; // 15 minutes

        private static PostProcessingBehaviour _ppb;
        private static bool _caWasEnabled;
        private static float _caOriginalIntensity;
        private static bool _caStateSaved;
        private static bool _lookupLogged;

        public static float GetDrunkMultiplier(Player player)
        {
            if (player == null) return 0f;

            var seDrunk = player.GetSEMan()?.GetStatusEffect("SE_Drunk".GetStableHashCode()) as SE_Drunk;
            if (seDrunk != null)
            {
                return seDrunk.GetTotalMultiplier();
            }

            return 0f;
        }

        public static void Postfix(GameCamera __instance)
        {
            var player = Player.m_localPlayer;
            float multiplier = GetDrunkMultiplier(player);

            if (multiplier <= 0f)
            {
                RestoreChromaticAberration();
                return;
            }

            ApplyWobble(__instance, multiplier);
            ApplyChromaticAberration(__instance, multiplier);
        }

        // Pitch/roll only — yaw drift is handled by DrunkYawDriftPatch, which
        // modifies m_lookYaw so it also affects movement direction.
        private static void ApplyWobble(GameCamera cam, float multiplier)
        {
            float t     = Time.time * WobbleSpeed;
            float amp   = BaseWobbleAmplitude * multiplier;
            float pitch = (Mathf.PerlinNoise(t,         0.5f) - 0.5f) * 2f * amp;
            float roll  = (Mathf.PerlinNoise(t + 100f,  0.5f) - 0.5f) * 2f * amp;

            cam.transform.rotation *= Quaternion.Euler(pitch, 0f, roll);
        }

        private static void ApplyChromaticAberration(GameCamera cam, float multiplier)
        {
            if (_ppb == null) _ppb = FindPostProcessingBehaviour(cam);
            if (_ppb?.profile?.chromaticAberration == null) return;

            var model = _ppb.profile.chromaticAberration;

            if (!_caStateSaved)
            {
                _caWasEnabled        = model.enabled;
                _caOriginalIntensity = model.settings.intensity;
                _caStateSaved        = true;

                // If the effect was disabled in the serialized profile, the behaviour's
                // internal component list was built without it; re-assigning the profile
                // forces OnDisable/OnEnable which rebuilds that list.
                if (!_caWasEnabled)
                {
                    var p = _ppb.profile;
                    _ppb.profile = null;
                    _ppb.profile = p;
                    Log.Debug("MeadCameraEffect: reassigned profile to force chromaticAberration init");
                }
            }

            float t        = Time.time * CASpeed;
            float intensity = Mathf.Clamp01(Mathf.Lerp(CAMin, CAMax, Mathf.PerlinNoise(t, 0.7f)) * multiplier);

            var settings      = model.settings;
            settings.intensity = intensity;
            model.settings    = settings;
            model.enabled     = true;
        }

        private static PostProcessingBehaviour FindPostProcessingBehaviour(GameCamera cam)
        {
            // PostProcessingBehaviour attaches to a Unity Camera. It may live on the
            // GameCamera root, a child, or on the active main Camera. Try in order
            // and log the outcome once so we can diagnose if it keeps missing.
            var direct = cam.GetComponent<PostProcessingBehaviour>();
            if (direct != null)
            {
                LogLookup("GetComponent on GameCamera", true, direct);
                return direct;
            }

            var child = cam.GetComponentInChildren<PostProcessingBehaviour>(includeInactive: true);
            if (child != null)
            {
                LogLookup("GetComponentInChildren on GameCamera", true, child);
                return child;
            }

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var onMain = mainCam.GetComponent<PostProcessingBehaviour>();
                if (onMain != null)
                {
                    LogLookup("GetComponent on Camera.main", true, onMain);
                    return onMain;
                }
            }

            var any = Object.FindObjectOfType<PostProcessingBehaviour>();
            if (any != null)
            {
                LogLookup($"FindObjectOfType on '{any.gameObject.name}'", true, any);
                return any;
            }

            LogLookup("all lookups failed", false, null);
            return null;
        }

        private static void LogLookup(string path, bool success, PostProcessingBehaviour ppb)
        {
            if (_lookupLogged) return;
            _lookupLogged = true;

            if (!success)
            {
                Log.Warn("MeadCameraEffect: PostProcessingBehaviour not found — " + path);
                return;
            }

            bool hasProfile = ppb.profile != null;
            bool hasCA = hasProfile && ppb.profile.chromaticAberration != null;
            Log.Debug($"MeadCameraEffect: PostProcessingBehaviour found via {path} (profile={hasProfile}, chromaticAberration={hasCA})");
        }

        private static void RestoreChromaticAberration()
        {
            if (!_caStateSaved) return;
            if (_ppb?.profile?.chromaticAberration == null)
            {
                _caStateSaved = false;
                return;
            }

            var model    = _ppb.profile.chromaticAberration;
            var settings = model.settings;
            settings.intensity = _caOriginalIntensity;
            model.settings     = settings;
            model.enabled      = _caWasEnabled;

            _ppb          = null;
            _caStateSaved = false;
            _lookupLogged = false;
        }
    }
}
