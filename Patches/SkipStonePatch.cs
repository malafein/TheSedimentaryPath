using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Attached to a thrown SmoothStone projectile on the owning client.
    // Tracks how many water skips have occurred so XP and damage can escalate.
    public class SkippingStone : MonoBehaviour
    {
        public int       SkipCount;
        public bool      FinalImpactHandled;
        public Character Owner;
    }

    // When a SmoothStone is thrown, opt the projectile into water detection
    // and set the skip cap via the existing m_maxBounces field.
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    public static class SkipStonePatch_Setup
    {
        static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            if (item?.m_shared?.m_name != "$item_smoothstone") return;

            var skip   = __instance.gameObject.AddComponent<SkippingStone>();
            skip.Owner = owner;

            __instance.m_canHitWater = true;
            __instance.m_maxBounces  = 3;

            PopulateWaterEffects(__instance);
        }

        private static void PopulateWaterEffects(Projectile projectile)
        {
            GameObject floatPrefab = ZNetScene.instance?.GetPrefab("FishingRodFloatProjectile");
            if (floatPrefab != null)
            {
                Projectile floatProjectile = floatPrefab.GetComponent<Projectile>();
                if (floatProjectile?.m_hitWaterEffects?.m_effectPrefabs?.Length > 0)
                {
                    projectile.m_hitWaterEffects.m_effectPrefabs = floatProjectile.m_hitWaterEffects.m_effectPrefabs;
                    Log.Debug($"SkipStonePatch: wired {floatProjectile.m_hitWaterEffects.m_effectPrefabs.Length} water effect(s) from FishingRodFloatProjectile.Projectile");
                    return;
                }

                Floating floating = floatPrefab.GetComponent<Floating>();
                if (floating?.m_impactEffects?.m_effectPrefabs?.Length > 0)
                {
                    projectile.m_hitWaterEffects.m_effectPrefabs = floating.m_impactEffects.m_effectPrefabs;
                    Log.Debug($"SkipStonePatch: wired {floating.m_impactEffects.m_effectPrefabs.Length} water effect(s) from FishingRodFloatProjectile.Floating");
                    return;
                }

                if (Plugin.IsDebugMode)
                {
                    var sb = new System.Text.StringBuilder("SkipStonePatch: FishingRodFloatProjectile has no water effects; components:");
                    foreach (Component c in floatPrefab.GetComponents<Component>())
                        sb.Append('\n').Append(c.GetType().Name);
                    Log.Debug(sb.ToString());
                }
            }
            else
            {
                Log.Debug("SkipStonePatch: FishingRodFloatProjectile not found in ZNetScene");
            }
        }
    }

    // Intercept Projectile.OnHit to handle skips and award XP.
    [HarmonyPatch(typeof(Projectile), "OnHit")]
    public static class SkipStonePatch_OnHit
    {
        private static readonly AccessTools.FieldRef<Projectile, Vector3> VelRef =
            AccessTools.FieldRefAccess<Projectile, Vector3>("m_vel");

        private static readonly AccessTools.FieldRef<Projectile, bool> DidHitRef =
            AccessTools.FieldRefAccess<Projectile, bool>("m_didHit");

        // Returns false on a successful skip — suppresses vanilla water-hit handling
        // so m_didHit stays false and the projectile lives on.
        static bool Prefix(Projectile __instance, Vector3 hitPoint, bool water)
        {
            var skip = __instance.GetComponent<SkippingStone>();
            if (skip == null) return true; // not our stone
            if (!water)       return true; // let vanilla handle terrain / creature hits

            ref Vector3 vel = ref VelRef(__instance);

            // Skips exhausted — let the stone sink (vanilla water hit runs)
            if (skip.SkipCount >= __instance.m_maxBounces) return true;

            // Stone slowed to a crawl — let it sink
            if (vel.magnitude < __instance.m_minBounceVel) return true;

            // Angle check: more than ~33° below horizontal won't skip
            if (-vel.normalized.y > 0.55f) return true;

            // Reflect velocity off the water surface and flatten the arc so it skims
            // forward rather than launching upward.
            float   speed     = vel.magnitude;
            Vector3 reflected = Vector3.Reflect(vel.normalized, Vector3.up);
            reflected.y      *= 0.4f;                          // flatten
            vel               = reflected.normalized * (speed * 0.65f); // dampen

            // Snap to the water surface so FixedUpdate's position check doesn't
            // re-fire OnHit on every frame while the stone climbs back above water.
            Vector3 pos      = __instance.transform.position;
            float   surface  = Floating.GetLiquidLevel(pos);
            if (pos.y < surface)
                __instance.transform.position = new Vector3(pos.x, surface + 0.05f, pos.z);

            // Each skip adds blunt damage (m_damage is a public struct field)
            __instance.m_damage.m_blunt += RockerySkill.SkipDamagePerSkip;

            // Water splash at the skip point
            __instance.m_hitWaterEffects.Create(hitPoint, Quaternion.identity);

            skip.SkipCount++;

            Log.Debug($"SkipStonePatch: skip #{skip.SkipCount} vel={vel.magnitude:F1} at {hitPoint}");

            return false; // stone is still alive — suppress vanilla hit/destroy
        }

        // Award Rockery XP once the stone truly lands (vanilla set m_didHit = true).
        // Fires after every OnHit call, including suppressed ones, so we gate on m_didHit.
        static void Postfix(Projectile __instance)
        {
            var skip = __instance.GetComponent<SkippingStone>();
            if (skip == null || skip.SkipCount == 0 || skip.FinalImpactHandled) return;
            if (!DidHitRef(__instance)) return; // prefix returned false (skip frame) — not done yet

            skip.FinalImpactHandled = true;
            float xp = skip.SkipCount * RockerySkill.SkipXPPerSkip;
            skip.Owner?.RaiseSkill(RockerySkill.SkillType, xp);

            Log.Debug($"SkipStonePatch: final impact: {skip.SkipCount} skip(s), blunt={__instance.m_damage.m_blunt:F1}, XP +{xp:F1}");
        }
    }
}
