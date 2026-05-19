using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks brew-batch collections for The Vine's Cup / The Stone's Cup feats.
    // Fermenter.DelayedTap is called once per collection by the ZDO owner — in
    // single-player that's the local player. Multiplayer caveat: the credit
    // currently goes to whoever owns the fermenter, not who tapped it. Phase 2
    // could route credit via RPC; for now this is fine for solo play.
    [HarmonyPatch(typeof(Fermenter), "DelayedTap")]
    public static class FermenterTapPatch
    {
        private static readonly AccessTools.FieldRef<Fermenter, string> DelayedTapItemRef =
            AccessTools.FieldRefAccess<Fermenter, string>("m_delayedTapItem");

        // Captured before DelayedTap runs in case it clears the field after spawning items.
        public static void Prefix(Fermenter __instance, out string __state)
        {
            __state = DelayedTapItemRef(__instance);
        }

        public static void Postfix(string __state)
        {
            Player player = Player.m_localPlayer;
            if (player == null || string.IsNullOrEmpty(__state)) return;

            if (__state == "BlackstoneBrewBase")
                FeatTracker.RecordEvent(player, Feats.StoneMeadFermented);
            else if (__state == "VineberryJuiceBase")
                FeatTracker.RecordEvent(player, Feats.VineJuiceFermented);
        }
    }
}
