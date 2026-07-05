using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // SharedData.m_attackStatusEffect is weapon-wide — both the primary and the
    // secondary attack read it when building HitData (baked at the animation hit
    // trigger / projectile fire, not in StartAttack). The vinery weapons want a
    // per-swing effect: the primary always snares, while the secondary's effect
    // depends on the active stance. StartAttack's secondaryAttack flag tells us which
    // is about to fire, so we set the right effect here, just before the swing.
    //
    //   RootAtgeir — primary/Sweep: snare;  Furrow secondary: root
    //   RootSpear  — primary/Vault: snare;  Cast secondary:   tether (reel)
    //                (Vault's self-pull is handled client-side by RootSpearProjectile)
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class HumanoidStartAttackPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Humanoid __instance, bool secondaryAttack)
        {
            if (__instance != Player.m_localPlayer) return;

            var shared = __instance.GetCurrentWeapon()?.m_shared;
            if (shared == null) return;
            if (!Plugin.StanceWeapons.TryGetValue(shared.m_name, out var weapon)) return;

            if (weapon is RootAtgeir atgeir)
                atgeir.PrepareAttackEffect(shared, secondaryAttack);
            else if (weapon is RootSpear spear)
                spear.PrepareAttackEffect(shared, secondaryAttack);
        }
    }
}
