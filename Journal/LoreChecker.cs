using System.Collections.Generic;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Event-driven lore dispatcher. Patches and journal-system code call
    // the Notify* methods when something the lore might care about
    // happens; LoreChecker consults LoreRegistry's inverted indexes,
    // evaluates candidate stages, and advances any entry whose next-stage
    // condition now passes.
    //
    // Stages advance strictly sequentially. A single dispatch can carry
    // an entry through multiple stages if several conditions all pass
    // (TryAdvanceAll loops until a stage condition fails).
    //
    // Save-loaded conditions that were met but never fired (e.g. lore
    // added in a mod update applied to an existing save) get picked up
    // by EvaluateAll, called once on Player.OnSpawned.
    public static class LoreChecker
    {
        // ── Event Notify entrypoints ─────────────────────────────────────

        // Fires on every feat increment / completionist-add / personal-
        // best, not just tier crossings — sub-tier FeatThreshold gates
        // (e.g. lore at counter=10 when the first feat tier is 50) need
        // event-time evaluation, and a dict miss on the cold path is
        // cheap enough that the flexibility wins.
        public static void NotifyFeatChanged(Player player, string featId)
        {
            if (player == null || string.IsNullOrEmpty(featId)) return;
            DispatchTo(player, LoreRegistry.GetByFeat(featId));
        }

        public static void NotifyFlag(Player player, string flagId)
        {
            if (player == null || string.IsNullOrEmpty(flagId)) return;
            DispatchTo(player, LoreRegistry.GetByFlag(flagId));
        }

        public static void NotifyBoonTier(Player player, string boonId)
        {
            if (player == null || string.IsNullOrEmpty(boonId)) return;
            DispatchTo(player, LoreRegistry.GetByBoon(boonId));
        }

        public static void NotifyRecipe(Player player, string recipeSharedName)
        {
            if (player == null || string.IsNullOrEmpty(recipeSharedName)) return;
            DispatchTo(player, LoreRegistry.GetByRecipe(recipeSharedName));
        }

        public static void NotifyBiome(Player player, Heightmap.Biome biome)
        {
            if (player == null) return;
            DispatchTo(player, LoreRegistry.GetByBiome(biome));
        }

        public static void NotifySkill(Player player, ValheimSkills.SkillType skill)
        {
            if (player == null) return;
            DispatchTo(player, LoreRegistry.GetBySkill(skill));
        }

        // ── One-shot evaluation pass (Player.OnSpawned) ──────────────────

        // Walks every registered entry and advances any stages whose
        // conditions are currently satisfied. Used at spawn time so
        // save-loaded state catches up to any lore that was added since
        // the save was last opened, or any condition that was met while
        // the player was offline / between sessions.
        public static void EvaluateAll(Player player)
        {
            if (player == null) return;
            int changes = 0;
            foreach (var entry in LoreRegistry.All())
                changes += TryAdvanceAll(entry, player);
            if (changes > 0)
                Log.Info($"LoreChecker: EvaluateAll caught {changes} unlock/advance(s) for {player.GetPlayerName()}");
        }

        // ── Internals ────────────────────────────────────────────────────

        private static void DispatchTo(Player player, IReadOnlyList<LoreStageRef> candidates)
        {
            // Multiple stages of one entry can share an event channel
            // (e.g. several FeatThreshold stages on the same feat).
            // Dedupe so TryAdvanceAll runs exactly once per entry.
            HashSet<string> seen = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                var entry = candidates[i].Entry;
                if (entry == null) continue;
                if (seen == null) seen = new HashSet<string>();
                if (!seen.Add(entry.Id)) continue;
                TryAdvanceAll(entry, player);
            }
        }

        // Returns the number of stages advanced for this entry on this call.
        private static int TryAdvanceAll(LoreEntry entry, Player player)
        {
            if (entry == null || entry.Stages.Count == 0 || player == null) return 0;

            int changes = 0;
            int currentStage;

            if (!JournalData.IsLoreUnlocked(player, entry.Id))
            {
                var stage0 = entry.Stages[0];
                if (stage0?.Condition == null || !stage0.Condition.Evaluate(player)) return 0;
                JournalData.UnlockLore(player, entry.Id);
                AnnounceUnlock(entry, 0);
                changes++;
                currentStage = 0;
            }
            else
            {
                currentStage = JournalData.GetLoreStage(player, entry.Id);
            }

            while (currentStage + 1 < entry.Stages.Count)
            {
                var nextStage = entry.Stages[currentStage + 1];
                if (nextStage?.Condition == null || !nextStage.Condition.Evaluate(player)) break;
                if (!JournalData.AdvanceLoreStage(player, entry.Id, currentStage + 1)) break;
                currentStage++;
                AnnounceUnlock(entry, currentStage);
                changes++;
            }

            return changes;
        }

        // Placeholder TSP-voice notification text — final wording lands
        // alongside the lore entries themselves in E.4d.
        private static void AnnounceUnlock(LoreEntry entry, int stageIdx)
        {
            string msg = stageIdx == 0
                ? $"the path reveals — {entry.Title}"
                : $"the path unfolds — {entry.Title}";
            Notify.Center(msg);
            Log.Info($"LoreChecker: '{entry.Id}' stage {stageIdx} unlocked");
        }
    }
}
