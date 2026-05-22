using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks distinct biomes entered for the Lands Walked feat.
    // AddKnownBiome fires exactly once per biome change (called from UpdateBiome
    // only when m_currentBiome differs from the new one), so this is the cleanest
    // single-shot hook.
    [HarmonyPatch(typeof(Player), "AddKnownBiome")]
    public static class PlayerBiomePatch
    {
        public static void Postfix(Player __instance, Heightmap.Biome biome)
        {
            if (__instance == null || __instance != Player.m_localPlayer) return;
            if (biome == Heightmap.Biome.None) return;

            // Storing as the enum's int value (stable, mod-friendly) rather than
            // the name string. Display layer can map back via enum.
            string biomeKey = ((int)biome).ToString();
            FeatTracker.AddDistinct(__instance, Feats.BiomesEntered, biomeKey);

            LoreChecker.NotifyBiome(__instance, biome);
        }
    }
}
