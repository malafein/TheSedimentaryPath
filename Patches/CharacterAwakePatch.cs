using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Stamps the summoner's Vinery skill factor into each staff-summoned
    // tentaroot's ZDO at creation (see NatureWeaponEmpowerment: the note is taken
    // by SpawnAbilityPatch on the caster's client; only that client both holds a
    // live note and owns the fresh ZDO, so remote instantiations and reloaded
    // summons pass through untouched).
    [HarmonyPatch(typeof(Character), "Awake")]
    public static class CharacterAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance)
        {
            if (!__instance.name.StartsWith(NatureWeaponEmpowerment.SummonPrefab)) return;

            NatureWeaponEmpowerment.StampSummonedRoot(__instance);
        }
    }
}
