using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Character.RPC_Damage Postfix.
    //
    // Two clients see this method run for any given damage event:
    //   1. The attacker's client (briefly — vanilla sets m_localPlayerHasHit
    //      before its IsOwner early-exit). We use this run to record the
    //      local player's boss-fight contribution.
    //   2. The ZDO owner's client (full pass — actual damage application).
    //      We use this run to accumulate damage facts on the creature's ZDO.
    //
    // Bystander clients (not attacker, not owner) never see RPC_Damage fire.
    //
    // Filter env / mob-on-mob damage and resolve attacker / owner / local
    // flags once here, then pass into AchievementSystem.RecordDamage so it
    // doesn't have to repeat the GetAttacker() lookup.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class CharacterDamagePatch
    {
        private static readonly AccessTools.FieldRef<Character, ZNetView> NviewRef =
            AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");

        public static void Postfix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || __instance is Player) return;

            Character attacker = hit.GetAttacker();
            if (!(attacker is Player playerAttacker)) return;

            Player local = Player.m_localPlayer;
            bool attackerIsLocal = local != null && playerAttacker == local;

            ZNetView nv = NviewRef(__instance);
            bool isOwner = nv != null && nv.IsOwner();

            // Nothing for us to do on a bystander instance (shouldn't happen
            // given RPC routing, but guard cheaply).
            if (!attackerIsLocal && !isOwner) return;

            AchievementSystem.RecordDamage(__instance, hit, isOwner, attackerIsLocal);
        }
    }
}
