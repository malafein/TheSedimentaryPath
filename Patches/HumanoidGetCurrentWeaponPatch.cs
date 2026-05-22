using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Items;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Substitutes KinFist for the local player's m_unarmedWeapon when
    // Stone-Kin is at tier 3 AND the doctrine predicate holds AND the
    // right hand is empty (so m_unarmedWeapon is the active weapon).
    //
    // The local player's actual m_unarmedWeapon is never mutated; we
    // just return a different ItemData from this postfix. When any
    // condition flips false, vanilla fists come back automatically.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetCurrentWeapon))]
    public static class HumanoidGetCurrentWeaponPatch
    {
        private static readonly int StoneKinSEHash = "SE_StoneKin".GetStableHashCode();

        public static void Postfix(Humanoid __instance, ref ItemDrop.ItemData __result)
        {
            if (!(__instance is Player player)) return;
            if (player != Player.m_localPlayer) return;
            if (player.RightItem != null) return;          // not bare-fisted
            if (!KinFist.IsReady) return;                  // build failed (FistFenrirClaw missing)

            SE_StoneKin se = player.GetSEMan()?.GetStatusEffect(StoneKinSEHash) as SE_StoneKin;
            if (se == null || se.Tier < 3) return;

            if (!StoneKinPredicate.IsActive(player)) return;

            __result = KinFist.ItemData;
        }
    }
}
