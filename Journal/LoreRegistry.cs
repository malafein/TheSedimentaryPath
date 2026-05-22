using System.Collections.Generic;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Pointer to a specific stage within a specific entry. Inverted-index
    // values hold these so an event-driven dispatch can locate the exact
    // candidate stage(s) without re-scanning entries.
    public readonly struct LoreStageRef
    {
        public readonly LoreEntry Entry;
        public readonly int       StageIndex;
        public LoreStageRef(LoreEntry entry, int stageIndex)
        {
            Entry      = entry;
            StageIndex = stageIndex;
        }
    }

    // Static catalog of every lore entry. Populated by TSPLore.RegisterAll()
    // at Plugin.Awake. Parallel to FeatRegistry / BoonRegistry.
    //
    // Maintains a primary id→entry map plus per-event-channel inverted
    // indexes so LoreChecker.Notify* methods can dispatch in O(1+ matches)
    // without scanning every entry on every game event.
    //
    // Each stage's condition contributes exactly one index entry (the
    // channel it cares about). The EvaluateAll spawn-pass in LoreChecker
    // is the only path that touches stages without going through an index.
    public static class LoreRegistry
    {
        private static readonly Dictionary<string, LoreEntry> _byId
            = new Dictionary<string, LoreEntry>();

        private static readonly Dictionary<string, List<LoreStageRef>> _byFeat
            = new Dictionary<string, List<LoreStageRef>>();
        private static readonly Dictionary<string, List<LoreStageRef>> _byFlag
            = new Dictionary<string, List<LoreStageRef>>();
        private static readonly Dictionary<string, List<LoreStageRef>> _byBoon
            = new Dictionary<string, List<LoreStageRef>>();
        private static readonly Dictionary<string, List<LoreStageRef>> _byRecipe
            = new Dictionary<string, List<LoreStageRef>>();
        private static readonly Dictionary<Heightmap.Biome, List<LoreStageRef>> _byBiome
            = new Dictionary<Heightmap.Biome, List<LoreStageRef>>();
        private static readonly Dictionary<ValheimSkills.SkillType, List<LoreStageRef>> _bySkill
            = new Dictionary<ValheimSkills.SkillType, List<LoreStageRef>>();

        private static readonly List<LoreStageRef> _empty = new List<LoreStageRef>();

        // ── Public API ───────────────────────────────────────────────────

        public static void Register(LoreEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return;
            _byId[entry.Id] = entry;

            for (int i = 0; i < entry.Stages.Count; i++)
            {
                IndexCondition(entry, i, entry.Stages[i]?.Condition);
            }

            Log.Debug($"LoreRegistry: registered '{entry.Id}' ({entry.Title}) with {entry.Stages.Count} stage(s)");
        }

        public static LoreEntry Get(string id)
        {
            _byId.TryGetValue(id, out LoreEntry entry);
            return entry;
        }

        public static IEnumerable<LoreEntry> All() => _byId.Values;

        // ── Index accessors (used by LoreChecker.Notify* dispatchers) ────

        public static IReadOnlyList<LoreStageRef> GetByFeat(string featId)
            => Lookup(_byFeat, featId);
        public static IReadOnlyList<LoreStageRef> GetByFlag(string flagId)
            => Lookup(_byFlag, flagId);
        public static IReadOnlyList<LoreStageRef> GetByBoon(string boonId)
            => Lookup(_byBoon, boonId);
        public static IReadOnlyList<LoreStageRef> GetByRecipe(string recipeSharedName)
            => Lookup(_byRecipe, recipeSharedName);
        public static IReadOnlyList<LoreStageRef> GetByBiome(Heightmap.Biome biome)
            => _byBiome.TryGetValue(biome, out var list) ? list : _empty;
        public static IReadOnlyList<LoreStageRef> GetBySkill(ValheimSkills.SkillType skill)
            => _bySkill.TryGetValue(skill, out var list) ? list : _empty;

        // ── Internals ────────────────────────────────────────────────────

        private static void IndexCondition(LoreEntry entry, int stageIdx, LoreCondition cond)
        {
            if (cond == null) return;
            var stageRef = new LoreStageRef(entry, stageIdx);

            switch (cond)
            {
                case FeatThreshold ft:
                    AddTo(_byFeat, ft.FeatId, stageRef);
                    break;
                case FeatCompletionistCount fc:
                    AddTo(_byFeat, fc.FeatId, stageRef);
                    break;
                case FirstTimeFlag flag:
                    AddTo(_byFlag, flag.FlagId, stageRef);
                    break;
                case BoonTierReached btr:
                    AddTo(_byBoon, btr.BoonId, stageRef);
                    break;
                case RecipeUnlocked rec:
                    AddTo(_byRecipe, rec.RecipeSharedName, stageRef);
                    break;
                case BiomeEntered biome:
                    AddTo(_byBiome, biome.Biome, stageRef);
                    break;
                case SkillLevel skill:
                    AddTo(_bySkill, skill.Skill, stageRef);
                    break;
                default:
                    Log.Warn($"LoreRegistry: unindexed condition type {cond.GetType().Name} on '{entry.Id}' stage {stageIdx} — will only fire on the EvaluateAll spawn pass");
                    break;
            }
        }

        private static void AddTo<TKey>(Dictionary<TKey, List<LoreStageRef>> index, TKey key, LoreStageRef stageRef)
        {
            if (key == null) return;
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<LoreStageRef>();
                index[key] = list;
            }
            list.Add(stageRef);
        }

        private static IReadOnlyList<LoreStageRef> Lookup(Dictionary<string, List<LoreStageRef>> index, string key)
        {
            if (string.IsNullOrEmpty(key)) return _empty;
            return index.TryGetValue(key, out var list) ? list : _empty;
        }
    }
}
