using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks distinct runestones read for the Stones That Speak feat.
    // Vegvisirs are included — they "speak" in the sense that they reveal a
    // location, fitting the animate-stone thread shared with They That Watch
    // and Stones That Travel.
    //
    // Key resolution: the *text* is what the player encountered, so we hash
    // m_text in every case. Stone-level m_label is intentionally NOT used —
    // vanilla reuses labels as internal category tags across multiple random
    // texts (e.g. $lore_meadows_boartaming_label appears with several
    // different m_text bodies), so keying on label collapses distinct lore
    // into one entry. For random-text stones we replicate vanilla's
    // deterministic position-seeded picker to find the actually-shown text.
    // Stones with no text at all (pure Vegvisirs) fall back to location name.
    [HarmonyPatch(typeof(RuneStone), nameof(RuneStone.Interact))]
    public static class RuneStonePatch
    {
        public static void Postfix(RuneStone __instance, Humanoid character, bool hold)
        {
            if (hold) return;
            if (!(character is Player player) || player != Player.m_localPlayer) return;
            if (__instance == null) return;

            string entryId = ResolveEntryId(__instance);
            if (entryId == null)
            {
                Log.Debug($"RuneStonePatch: no entryId resolved for runestone at {__instance.transform.position} " +
                          $"(label='{__instance.m_label}', text-len={(__instance.m_text?.Length ?? 0)}, " +
                          $"randomTexts={(__instance.m_randomTexts?.Count ?? 0)}, locName='{__instance.m_locationName}')");
                return;
            }

            FeatTracker.AddDistinct(player, Feats.RunestonesRead, entryId);
        }

        private static string ResolveEntryId(RuneStone stone)
        {
            // Vanilla's GetRandomText picks deterministically from position, so
            // re-running its logic gives us the same RandomRuneText the player
            // actually saw. Save/restore Random.state to mirror vanilla.
            if (stone.m_randomTexts != null && stone.m_randomTexts.Count > 0)
            {
                Vector3 pos = stone.transform.position;
                int seed = (int)pos.x * (int)pos.z;
                Random.State prev = Random.state;
                Random.InitState(seed);
                int idx = Random.Range(0, stone.m_randomTexts.Count);
                Random.state = prev;

                RuneStone.RandomRuneText chosen = stone.m_randomTexts[idx];
                if (!string.IsNullOrEmpty(chosen.m_text))
                    return chosen.m_text.GetStableHashCode().ToString();
                // Fall through if the chosen entry has no body text.
            }

            if (!string.IsNullOrEmpty(stone.m_text))
                return stone.m_text.GetStableHashCode().ToString();
            if (!string.IsNullOrEmpty(stone.m_locationName))
                return "loc:" + stone.m_locationName;

            return null;
        }
    }
}
