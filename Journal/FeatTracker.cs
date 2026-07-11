using System.Collections.Generic;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    public enum FeatCategory
    {
        StonePath,
        VinePath,
        Ferment,
        Pilgrimages,
        Trials
    }

    public enum FeatShape
    {
        TieredCounter,
        UntieredRecord,
        CompletionistSet
    }

    // How a feat's stored value is rendered in the journal.
    //   Number   — the raw integer (default)
    //   GameTime — the value is real-seconds; show humanized in-game time
    //              (e.g. "3 days, 12 hours"). Used by drunk_seconds.
    //   Distance — the value is whole meters; show m / km (e.g. "1.2 km").
    //              Used by the travel feats (sailed / walked-as-distance /
    //              ridden) and altitude.
    public enum DisplayFormat
    {
        Number,
        GameTime,
        Distance
    }

    public class FeatDef
    {
        public string Id { get; }
        public string Name { get; }
        public FeatCategory Category { get; }
        public FeatShape Shape { get; }
        public int[] Thresholds { get; }
        public string TriggerDescription { get; }
        public DisplayFormat Display { get; }

        public FeatDef(string id, string name, FeatCategory category, FeatShape shape,
                       int[] thresholds, string triggerDescription,
                       DisplayFormat display = DisplayFormat.Number)
        {
            Id = id;
            Name = name;
            Category = category;
            Shape = shape;
            Thresholds = thresholds ?? new int[0];
            TriggerDescription = triggerDescription;
            Display = display;
        }
    }

    // Static catalog of every feat. Loaded once at type init.
    public static class FeatRegistry
    {
        private static readonly Dictionary<string, FeatDef> _byId
            = new Dictionary<string, FeatDef>();

        static FeatRegistry()
        {
            // ── The Stone Path ─────────────────────────────────────────
            Add(Feats.RocksCollected,        "Stones at Hand",            FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 50, 500, 5000 },     "Rockery-list Pickable pickup");
            Add(Feats.StoneKills,            "Stone's Answer",            FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 50, 250, 2500 },     "Creature killed by TSP Rockery weapon");
            Add(Feats.SkipsAchieved,         "Stones That Travel",        FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 50, 250, 1000 },     "Total skip count summed across throws");
            Add(Feats.MaxSingleThrowSkips,   "One Stone, Many Steps",     FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 1, 3, 5 },           "Personal best skips on single throw");
            Add(Feats.EnemiesKilledBySkip,   "Met While Travelling",      FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 1, 10, 50 },         "Creature killed mid-skip");
            Add(Feats.StoneWeaponsCrafted,   "The Knapping",              FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 1, 10, 50 },         "TSP stone weapon crafted");
            Add(Feats.MysteriousRockFound,   "A Stone Looks Back",        FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 1, 5, 12 },          "Mysterious Rock found");
            Add(Feats.DistinctRockTypes,     "The Many Stones",           FeatCategory.StonePath, FeatShape.CompletionistSet, new[] { 1, 3, 6 },           "Distinct rock type picked up");
            Add(Feats.RockerySkillLevel,     "The Stone Grows Familiar",  FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 25, 50, 75, 100 },   "Rockery skill milestone");
            Add(Feats.MysteriousRocksPlaced, "They That Watch",           FeatCategory.StonePath, FeatShape.TieredCounter,    new[] { 1, 5, 10 },          "Mysterious Rock net-placed");

            // ── The Vine Path ──────────────────────────────────────────
            Add(Feats.VineWatchSeconds,      "The Verdant Vigil",         FeatCategory.VinePath,  FeatShape.UntieredRecord,   new int[0],                  "Time spent watching vines",              DisplayFormat.GameTime);
            Add(Feats.VinesGrown,            "Patience in Bloom",         FeatCategory.VinePath,  FeatShape.TieredCounter,    new[] { 5, 25, 100 },        "Vine matured under your watch");
            Add(Feats.VineberriesHarvested,  "The Vine's Gift",           FeatCategory.VinePath,  FeatShape.TieredCounter,    new[] { 50, 250, 1000 },     "Vine-borne pickable harvested (vineberry or bindsinew)");
            Add(Feats.VinerySkillLevel,      "The Vine Grows Familiar",   FeatCategory.VinePath,  FeatShape.TieredCounter,    new[] { 25, 50, 75, 100 },   "Vinery skill milestone");
            Add(Feats.VinesPlanted,          "The Sowing",                FeatCategory.VinePath,  FeatShape.TieredCounter,    new[] { 10, 50, 200 },       "Vine seed planted (ivy or vineberry)");
            // Bump thresholds if a third vinery weapon lands.
            Add(Feats.VineWeaponsCrafted,    "The Twining",               FeatCategory.VinePath,  FeatShape.CompletionistSet, new[] { 1, 2 },              "Distinct TSP vinery weapon crafted");
            Add(Feats.VineWeaponKills,       "The Vine's Answer",         FeatCategory.VinePath,  FeatShape.TieredCounter,    new[] { 50, 250, 2500 },     "Creature killed by TSP Vinery weapon");

            // ── The Ferment ────────────────────────────────────────────
            Add(Feats.BrewsConsumed,         "The Iron Gut of the Earth", FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 5, 50, 500 },        "TSP brew consumed");
            Add(Feats.PukeCount,             "The Bitter Offering",       FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 1, 10, 50 },         "Player puked");
            Add(Feats.BerriesForagedDrunk,   "The Swaying Harvest",       FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 10, 50, 250 },       "Berry picked while drunk");
            Add(Feats.FishCaughtDrunk,       "The Listing Cast",          FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 5, 25, 100 },        "Fish caught while drunk");
            Add(Feats.EnemiesKilledDrunk,    "The Unsteady Hand",         FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 25, 100, 500 },      "Creature killed while drunk");
            Add(Feats.RocksCollectedDrunk,   "The Reeling Haul",          FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 10, 100, 1000 },     "Rockery-list pickup while drunk");
            Add(Feats.DrunkSeconds,          "Days in the Cup",           FeatCategory.Ferment,   FeatShape.UntieredRecord,   new int[0],                  "Time spent drunk",                       DisplayFormat.GameTime);
            Add(Feats.DrunkCrafting,         "The Tilted Craft",          FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 1, 25, 100 },        "Item crafted while drunk");
            Add(Feats.DrunkSleeps,           "Spirits' Respite",          FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 1, 10, 50 },         "Sleep cycle completed while drunk");
            Add(Feats.VineJuiceFermented,    "The Vine's Cup",            FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 5, 25, 100 },        "Vineberry Juice batch collected");
            Add(Feats.StoneMeadFermented,    "The Stone's Cup",           FeatCategory.Ferment,   FeatShape.TieredCounter,    new[] { 5, 25, 100 },        "Blackstone Brew batch collected");
            // brews_variety: deferred until ≥4 TSP brews exist; registered with empty thresholds.
            Add(Feats.BrewsVariety,          "(name TBD)",                FeatCategory.Ferment,   FeatShape.CompletionistSet, new int[0],                  "Distinct TSP brew tasted (deferred)");

            // ── Pilgrimages ────────────────────────────────────────────
            Add(Feats.BossesDefeated,        "The Forsaken Felled",       FeatCategory.Pilgrimages, FeatShape.CompletionistSet, new[] { 1, 4, 8 },          "Distinct boss killed (IsBoss)");
            // Tier 1 starts at 2 — the spawn biome is a freebie and shouldn't count as a pilgrimage.
            Add(Feats.BiomesEntered,         "Lands Walked",              FeatCategory.Pilgrimages, FeatShape.CompletionistSet, new[] { 2, 5, 9 },          "Distinct biome entered");
            Add(Feats.RunestonesRead,        "Stones That Speak",         FeatCategory.Pilgrimages, FeatShape.CompletionistSet, new[] { 1, 15, 34 },        "Distinct runestone read");
            Add(Feats.RocksInDistantLands,   "The Far Placing",           FeatCategory.Pilgrimages, FeatShape.CompletionistSet, new[] { 1, 5, 9 },          "Mysterious Rock placed in distinct biome");
            Add(Feats.TradersVisited,        "Strangers Met",             FeatCategory.Pilgrimages, FeatShape.CompletionistSet, new[] { 1, 2, 3 },          "Distinct trader interacted");
            Add(Feats.SeaDistanceSailed,     "The Salt Path",             FeatCategory.Pilgrimages, FeatShape.TieredCounter,    new[] { 1000, 10000, 100000 }, "Cumulative distance sailed (m)",          DisplayFormat.Distance);
            Add(Feats.DistanceWalked,        "The Trodden Path",          FeatCategory.Pilgrimages, FeatShape.TieredCounter,    new[] { 1000, 25000, 150000 }, "Cumulative distance walked on foot (m)", DisplayFormat.Distance);
            Add(Feats.DistanceRidden,        "Borne by Beasts",           FeatCategory.Pilgrimages, FeatShape.TieredCounter,    new[] { 500, 5000, 25000 },    "Cumulative distance ridden on a mount",  DisplayFormat.Distance);
            Add(Feats.SwimDistance,          "The Long Swim",             FeatCategory.Pilgrimages, FeatShape.TieredCounter,    new[] { 500, 5000, 25000 },    "Cumulative distance swum (m)",           DisplayFormat.Distance);
            Add(Feats.HighestAltitude,       "Nearer the Sky",            FeatCategory.Pilgrimages, FeatShape.TieredCounter,    new[] { 50, 120, 200 },        "Highest ground reached (m above the sea)", DisplayFormat.Distance);

            // ── Trials ─────────────────────────────────────────────────
            // stone_only_creatures_felled "all" threshold is a placeholder of 50; TODO: revise once curated creature list is finalized.
            Add(Feats.StoneOnlyCreaturesFelled, "Stone, And Stone Alone",    FeatCategory.Trials, FeatShape.CompletionistSet, new[] { 1, 10, 50 },       "Distinct creature killed stone-only");
            Add(Feats.KinOnlyGolemKills,        "Brother Felled by Brother", FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1, 5, 20 },        "Stone Golem killed using only kin damage — stone or bare-fist (boon)");
            Add(Feats.PerfectSkips,             "The Full Travelling",       FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1, 10, 50 },       "Max-bounce skip on single throw");
            Add(Feats.PeakReached,              "Nothing Stood Higher",      FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1 },               "Stood atop the world's highest peak");
            Add(Feats.WatcherAtPeak,            "It Rests Easy",             FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1 },               "Placed a Watcher atop the world's highest peak");
            Add(Feats.DrunkPilgrimBosses,       "The Pilgrim's Cup",         FeatCategory.Trials, FeatShape.CompletionistSet, new[] { 1, 4, 8 },         "Boss defeated continuously drunk");
            Add(Feats.OneShotKills,             "One Stone, One Death",      FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1, 25, 100 },      "Creature killed by single thrown stone");
            Add(Feats.BossesUnarmored,          "Bared to the Forsaken",     FeatCategory.Trials, FeatShape.CompletionistSet, new[] { 1, 4, 8 },         "Boss defeated with empty armor slots");
            Add(Feats.BossesStoneOnly,          "The Forsaken Stone-Felled", FeatCategory.Trials, FeatShape.CompletionistSet, new[] { 1, 4, 8 },         "Boss defeated stone-only");
            Add(Feats.GolemUnarmedSurvived,     "Standing Before the Stone", FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 60, 180, 600 },    "Seconds unarmed in Stone Golem aggro range");
            Add(Feats.AbomRootedKills,          "The Grasping, Grasped",     FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 1, 5, 20 },        "Abomination felled while held by your roots");
            Add(Feats.RootedCreaturesFelled,    "Rooted, And Reaped",        FeatCategory.Trials, FeatShape.CompletionistSet, new[] { 1, 10, 50 },       "Distinct creature felled while held by your roots");
            Add(Feats.AbomStillSeconds,         "Taking Root",               FeatCategory.Trials, FeatShape.TieredCounter,    new[] { 30, 90, 300 },     "Seconds standing motionless before an Abomination");

            Log.Debug($"FeatRegistry: loaded {_byId.Count} feat definitions");
        }

        private static void Add(string id, string name, FeatCategory category, FeatShape shape,
                                int[] thresholds, string triggerDescription,
                                DisplayFormat display = DisplayFormat.Number)
            => _byId[id] = new FeatDef(id, name, category, shape, thresholds, triggerDescription, display);

        public static FeatDef Get(string id)
        {
            _byId.TryGetValue(id, out FeatDef def);
            return def;
        }

        public static IEnumerable<FeatDef> All() => _byId.Values;

        public static IEnumerable<FeatDef> ByCategory(FeatCategory category)
        {
            foreach (FeatDef def in _byId.Values)
                if (def.Category == category) yield return def;
        }
    }

    // Public API. Patches call into these to record that something happened.
    public static class FeatTracker
    {
        private static readonly int DrunkSEHash = "SE_Drunk".GetStableHashCode();

        // True if the player currently has the umbrella SE_Drunk effect active.
        // Used by every "X while drunk" feat in the Ferment category.
        public static bool IsDrunk(Player player)
            => player?.GetSEMan()?.GetStatusEffect(DrunkSEHash) != null;

        // Current tier reached for a tiered counter or completionist feat.
        // 0 = below the first threshold; 1..N = thresholds crossed.
        // For completionist feats the comparison is against set count, not raw value.
        // For untiered records returns 0 (no tiers defined).
        public static int GetCurrentTier(Player player, string featId)
        {
            FeatDef def = FeatRegistry.Get(featId);
            if (def == null || def.Thresholds.Length == 0) return 0;

            int value = def.Shape == FeatShape.CompletionistSet
                ? JournalData.GetCompletionistCount(player, featId)
                : JournalData.GetFeat(player, featId);

            int tier = 0;
            for (int i = 0; i < def.Thresholds.Length; i++)
                if (value >= def.Thresholds[i]) tier = i + 1;
            return tier;
        }

        // Increment a tiered counter (or untiered record) by delta.
        // No-op for completionist feats — use AddDistinct for those.
        //
        // quiet=true suppresses the per-increment Debug log line. Use for
        // high-frequency callers (e.g. per-meter sailing distance) where the
        // log would otherwise flood; tier crossings still log at Info.
        public static void RecordEvent(Player player, string featId, int delta = 1, bool quiet = false)
        {
            if (player == null || string.IsNullOrEmpty(featId) || delta == 0) return;

            FeatDef def = FeatRegistry.Get(featId);
            if (def == null)
            {
                Log.Warn($"FeatTracker.RecordEvent: unknown feat '{featId}'");
                return;
            }

            if (def.Shape == FeatShape.CompletionistSet)
            {
                Log.Warn($"FeatTracker.RecordEvent: feat '{featId}' is completionist; use AddDistinct");
                return;
            }

            int oldValue = JournalData.GetFeat(player, featId);
            int newValue = JournalData.IncrementFeat(player, featId, delta);

            if (!quiet)
                Log.Debug($"FeatTracker: {featId} {oldValue} -> {newValue}");

            if (def.Shape == FeatShape.TieredCounter)
                EvaluateTierCrossings(player, def, oldValue, newValue);

            LoreChecker.NotifyFeatChanged(player, featId);
        }

        // Add a distinct entry to a completionist set. Returns true if newly added.
        public static bool AddDistinct(Player player, string featId, string entryId)
        {
            if (player == null || string.IsNullOrEmpty(featId) || string.IsNullOrEmpty(entryId))
                return false;

            FeatDef def = FeatRegistry.Get(featId);
            if (def == null)
            {
                Log.Warn($"FeatTracker.AddDistinct: unknown feat '{featId}'");
                return false;
            }

            if (def.Shape != FeatShape.CompletionistSet)
            {
                Log.Warn($"FeatTracker.AddDistinct: feat '{featId}' is not completionist (shape={def.Shape})");
                return false;
            }

            if (!JournalData.AddCompletionistEntry(player, featId, entryId))
                return false;

            int newCount = JournalData.GetCompletionistCount(player, featId);
            Log.Debug($"FeatTracker: {featId} += '{entryId}' (count={newCount})");

            EvaluateTierCrossings(player, def, newCount - 1, newCount);
            LoreChecker.NotifyFeatChanged(player, featId);
            return true;
        }

        // Add seconds to an untiered record (or tiered counter via integer rounding).
        // Caller is responsible for fractional accumulation if precision matters.
        public static void AddSeconds(Player player, string featId, float dt)
        {
            int seconds = Mathf.RoundToInt(dt);
            if (seconds <= 0) return;
            RecordEvent(player, featId, seconds);
        }

        // Personal-best (monotonic high-water-mark) for tiered counters.
        // Used by max_single_throw_skips and similar.
        public static void RecordPersonalBest(Player player, string featId, int value)
        {
            if (player == null || string.IsNullOrEmpty(featId) || value <= 0) return;

            int current = JournalData.GetFeat(player, featId);
            if (value <= current) return;

            FeatDef def = FeatRegistry.Get(featId);
            if (def == null)
            {
                Log.Warn($"FeatTracker.RecordPersonalBest: unknown feat '{featId}'");
                return;
            }

            JournalData.SetFeat(player, featId, value);
            Log.Debug($"FeatTracker: {featId} personal best {current} -> {value}");

            if (def.Shape == FeatShape.TieredCounter)
                EvaluateTierCrossings(player, def, current, value);

            LoreChecker.NotifyFeatChanged(player, featId);
        }

        private static void EvaluateTierCrossings(Player player, FeatDef def, int oldValue, int newValue)
        {
            for (int i = 0; i < def.Thresholds.Length; i++)
            {
                int threshold = def.Thresholds[i];
                if (oldValue < threshold && newValue >= threshold)
                {
                    int tier = i + 1;
                    NotifyTierCrossed(player, def, tier);
                }
            }
        }

        private static void NotifyTierCrossed(Player player, FeatDef def, int tier)
        {
            string msg = $"{def.Name} — tier {tier}";
            Log.Info($"FeatTracker: tier crossed: {def.Id} tier={tier} ({def.Name})");
            Notify.Center(msg);
        }
    }
}
