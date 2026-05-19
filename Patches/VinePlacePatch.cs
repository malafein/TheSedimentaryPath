using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks vine-sapling placements for The Sowing feat.
    // Uses VinerySkill.IsVinePlant component-based detection — works for any
    // current or future vine without prefab-name matching.
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    public static class VinePlacePatch
    {
        public static void Postfix(Player __instance, Piece piece, bool __result)
        {
            if (!__result || piece == null) return;
            if (__instance != Player.m_localPlayer) return;

            Plant plant = piece.GetComponent<Plant>();
            if (!VinerySkill.IsVinePlant(plant)) return;

            FeatTracker.RecordEvent(__instance, Feats.VinesPlanted);
        }
    }
}
