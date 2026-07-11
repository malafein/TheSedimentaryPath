using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Character.Damage Postfix — attacker-side contribution recording.
    //
    // Character.Damage is the local entry point invoked by the attack/projectile
    // code on the client that simulated the hit (the attacker's own client). It
    // then routes RPC_Damage to the victim's ZDO owner. Because that routing is
    // owner-targeted, RPC_Damage never runs on a non-owner attacker — so the
    // attacker's participation can only be observed here, before the RPC leaves.
    //
    // Recording it here makes participation feats (stone_only_creatures_felled,
    // bosses_defeated / bosses_stone_only / bosses_unarmored / drunk_pilgrim,
    // kin_only_golem_kills) credit every player who landed a hit, not just the
    // one who happened to own the creature's ZDO (the closest / initiating
    // player) — which was the previous owner-only behaviour.
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class CharacterDamageLocalPatch
    {
        public static void Postfix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || __instance is Player) return;

            Player local = Player.m_localPlayer;
            if (local == null) return;

            // Cheap ZDOID compare — avoids resolving the attacker instance.
            if (hit.m_attacker != local.GetZDOID()) return;

            AchievementSystem.RecordLocalContribution(__instance);
            AchievementSystem.RecordLocalGrasp(__instance, hit);
        }
    }
}
