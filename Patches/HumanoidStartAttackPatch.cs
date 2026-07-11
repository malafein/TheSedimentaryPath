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
    // POSTFIX gated on __result: StartAttack is called repeatedly (held button,
    // retries mid-attack) and only sometimes starts a swing — routing on every call
    // re-rolled the Harrow root proc several times per swing. The successful call
    // returns true, and the hit trigger that reads the effect comes later in the
    // animation, so routing here is exactly once per real swing and still in time.
    //
    //   RootAtgeir — primary/Sweep: snare;  Furrow secondary: root (chance-rolled)
    //   RootSpear  — primary/Vault: snare;  Cast secondary:   tether (reel)
    //                (Vault's self-pull is handled client-side by RootSpearProjectile)
    //
    // The same per-swing timing also carries the jade empowerment
    // (NatureWeaponEmpowerment): the local player's Ashlands Nature weapons get
    // their proc chance scaled by Vinery, and staff-summoned tentaroots — whose
    // AI calls StartAttack on their owning client — get theirs scaled by the
    // summoner's stamped skill.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    public static class HumanoidStartAttackPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            if (!__result) return;

            var shared = __instance.GetCurrentWeapon()?.m_shared;
            if (shared == null) return;

            if (__instance != Player.m_localPlayer)
            {
                NatureWeaponEmpowerment.EmpowerSummonSwing(__instance, shared);
                return;
            }

            NatureWeaponEmpowerment.EmpowerPlayerSwing(shared);

            if (!Plugin.StanceWeapons.TryGetValue(shared.m_name, out var weapon)) return;

            if (weapon is RootAtgeir atgeir)
                atgeir.PrepareAttackEffect(shared, secondaryAttack);
            else if (weapon is RootSpear spear)
                spear.PrepareAttackEffect(shared, secondaryAttack);
        }
    }
}
