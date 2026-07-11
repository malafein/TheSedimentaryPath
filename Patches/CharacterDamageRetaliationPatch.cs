using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Holdfast's grasping retaliation — defender-side hook. RPC_Damage runs
    // on the victim's ZDO owner, and the local player always owns itself, so
    // every hit that reaches the local player arrives here — INCLUDING fully
    // blocked ones (BlockAttack runs inside the method body and the flow
    // continues): the vine grabs what reaches for you, and the shield loop
    // is deliberate.
    //
    // Separate patch class from CharacterDamagePatch: that one is the
    // owner-side ZDO accumulator for creatures and explicitly skips Player
    // victims; this one only cares about the local player as victim.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class CharacterDamageRetaliationPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance, HitData hit)
        {
            if (hit == null) return;
            if (!(__instance is Player player) || player != Player.m_localPlayer) return;

            // Melee only — an archer has touched no vine. Attacker-less hits
            // (falls, DoT ticks, environment) have no one to grasp.
            if (hit.m_ranged) return;
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker is Player) return;

            // A dodged hit never touched you — mirror RPC_Damage's own gate.
            if (hit.m_dodgeable && player.IsDodgeInvincible()) return;

            SE_Holdfast holdfast = player.GetSEMan()?.GetStatusEffect(SE_Holdfast.Hash) as SE_Holdfast;
            holdfast?.TryRetaliate(attacker);
        }
    }
}
