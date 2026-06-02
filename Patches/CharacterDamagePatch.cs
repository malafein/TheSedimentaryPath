using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Character.RPC_Damage Postfix — owner-side ZDO accumulation.
    //
    // ZNetView.InvokeRPC("RPC_Damage", ...) targets the victim's ZDO owner, so
    // this method runs ONLY on the owner's client (plus the attacker's client
    // in the case where the attacker is also the owner). The owner is the only
    // client that actually applies the hit, so it's the right place to
    // accumulate the per-fight damage facts that live on the creature's ZDO.
    //
    // The attacker-side contribution (which credits non-owner participants) is
    // recorded separately in CharacterDamageLocalPatch (Character.Damage),
    // because RPC_Damage never runs on a non-owner attacker's client.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class CharacterDamagePatch
    {
        private static readonly AccessTools.FieldRef<Character, ZNetView> NviewRef =
            AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");

        public static void Postfix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || __instance is Player) return;

            ZNetView nv = NviewRef(__instance);
            if (nv == null || !nv.IsOwner()) return;

            // Only player-attributed damage shapes the per-fight invariants.
            if (!(hit.GetAttacker() is Player)) return;

            AchievementSystem.RecordOwnerDamage(__instance, hit);
        }
    }
}
