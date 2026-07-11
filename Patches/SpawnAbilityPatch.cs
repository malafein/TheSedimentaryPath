using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // The Staff of the Wilds has no on-swing root proc to empower — its Vinery
    // bonus rides the summoned tentaroots instead (see NatureWeaponEmpowerment).
    // When the staff's projectile lands, Projectile.SpawnOnHit instantiates the
    // spawn object and hands it the caster via SpawnAbility.Setup — on the
    // caster's own client. That makes Setup the one place the summoner and the
    // summons meet: note the caster's skill factor here; CharacterAwakePatch
    // stamps it into each tentaroot's ZDO.
    //
    // PREFIX, not postfix: Setup's StartCoroutine("Spawn") runs the coroutine
    // synchronously up to its first yield, and with the staff's zero spawn
    // delays the tentaroot's Character.Awake fires INSIDE the Setup call — a
    // postfix would note the caster one landing too late (first cast's roots
    // went unstamped in testing).
    [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
    public static class SpawnAbilityPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SpawnAbility __instance, Character owner)
        {
            // Local caster only. Today this is guaranteed by the game (Projectile's
            // m_owner is a plain field set only on the firing client and never
            // restored from the ZDO — a remote Player can't reach here), but skills
            // aren't synced, so if a future game patch ever changed that, noting a
            // remote Player would silently read skill 0. Cheap to pin the invariant.
            if (!(owner is Player caster) || caster != Player.m_localPlayer) return;
            if (!__instance.name.StartsWith(NatureWeaponEmpowerment.SummonSpawnPrefab)) return;

            NatureWeaponEmpowerment.NoteSummonCast(caster);
        }
    }
}
