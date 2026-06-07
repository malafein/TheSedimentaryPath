using System.Collections;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    // The shrine pilgrimage hint: petting a Watcher silently sweeps the local
    // player's view toward the world's highest peak (the summit the world-height
    // scan found) - Vegvisir-in-NoMap style guidance, no map/compass/distance
    // text. It also sets the lore hint flag the first time, so The High Places
    // entry unlocks its "carry it to the Throat of the World" tease.
    public static class PeakGuide
    {
        // Per-frame slerp fraction toward the target; higher = snappier. Tuned
        // for a guided ~half-second sweep that still feels deliberate.
        private const float TurnSpeed   = 7f;
        private const float MaxDuration = 1.5f;   // safety cap on the sweep
        private const float StopAngle   = 1.5f;   // close enough -> done

        // Player look is private: m_lookYaw (yaw-only Quaternion) and m_lookPitch
        // (degrees; positive = look down). m_eye.rotation = m_lookYaw * Euler(pitch).
        private static readonly AccessTools.FieldRef<Player, Quaternion> YawRef =
            AccessTools.FieldRefAccess<Player, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> PitchRef =
            AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        private static Coroutine _sweep;

        public static void Offer(Player player, Vector3 from)
        {
            if (player == null || player != Player.m_localPlayer) return;
            if (!WorldData.ScanComplete || WorldData.MaxMountainElevation <= 0f) return;

            // First pet that points the way unlocks the lore tease.
            JournalData.SetFlag(player, TSPLore.PeakHintFlag);

            if (Plugin.Instance == null) return;
            if (_sweep != null) Plugin.Instance.StopCoroutine(_sweep);
            _sweep = Plugin.Instance.StartCoroutine(Sweep(player));
        }

        private static IEnumerator Sweep(Player player)
        {
            Vector3 peak = new Vector3(
                WorldData.MaxMountainXZ.x,
                WorldData.MaxMountainElevation,
                WorldData.MaxMountainXZ.y);

            float elapsed = 0f;
            while (elapsed < MaxDuration)
            {
                if (player == null || player != Player.m_localPlayer) yield break;

                // Recompute the target from the current eye each frame so the
                // sweep tracks correctly even if the player moves mid-turn.
                Vector3 eye = player.m_eye != null ? player.m_eye.position : player.transform.position + Vector3.up * 1.5f;
                Vector3 d = peak - eye;
                if (d.sqrMagnitude < 0.001f) yield break;
                d.Normalize();

                float targetYawDeg   = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float targetPitchDeg = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg, -89f, 89f);
                Quaternion targetYaw = Quaternion.Euler(0f, targetYawDeg, 0f);

                float k = Mathf.Clamp01(Time.deltaTime * TurnSpeed);
                Quaternion newYaw = Quaternion.Slerp(YawRef(player), targetYaw, k);
                float newPitch = Mathf.Lerp(PitchRef(player), targetPitchDeg, k);

                YawRef(player)   = newYaw;
                PitchRef(player) = newPitch;

                if (Quaternion.Angle(newYaw, targetYaw) < StopAngle
                    && Mathf.Abs(newPitch - targetPitchDeg) < StopAngle)
                {
                    YawRef(player)   = targetYaw;
                    PitchRef(player) = targetPitchDeg;
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _sweep = null;
        }
    }
}
