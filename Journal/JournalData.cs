using System.Collections.Generic;
using System.Globalization;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Typed read/write layer over Player.m_customData for journal state.
    //
    // Storage key namespace:
    //   TSP_journal_feat_<id>         int progress counter
    //   TSP_journal_lore_unlocked     comma-separated list of unlocked lore IDs
    //   TSP_journal_lore_stage_<id>   int, current stage of an evolving entry
    //   TSP_journal_boon_<id>         int, current unlocked boon tier (0 = locked)
    //   TSP_journal_flags             comma-separated one-time event flags
    //
    // All methods are null-safe; if the player is null or the key absent,
    // they return zero/false/empty without touching state.
    //
    // Stage, tier, and flag setters are monotonic: they never roll a value
    // backward, and their boolean return signals "was this a new advance?"
    // so callers can fire a notification without re-querying.
    public static class JournalData
    {
        private const string Prefix              = "TSP_journal_";
        private const string FeatPrefix          = Prefix + "feat_";
        private const string LoreStagePrefix     = Prefix + "lore_stage_";
        private const string BoonPrefix          = Prefix + "boon_";
        private const string CompletionistPrefix = Prefix + "set_";
        private const string LoreUnlockedKey     = Prefix + "lore_unlocked";
        private const string FlagsKey            = Prefix + "flags";

        // ── Feats ────────────────────────────────────────────────────────

        public static int GetFeat(Player player, string featId)
            => ReadInt(player, FeatPrefix + featId);

        public static void SetFeat(Player player, string featId, int value)
            => WriteInt(player, FeatPrefix + featId, value);

        public static int IncrementFeat(Player player, string featId, int delta = 1)
        {
            if (player == null) return 0;
            int next = ReadInt(player, FeatPrefix + featId) + delta;
            WriteInt(player, FeatPrefix + featId, next);
            return next;
        }

        // ── Lore unlocked (membership set) ──────────────────────────────

        public static bool IsLoreUnlocked(Player player, string loreId)
            => ContainsInList(player, LoreUnlockedKey, loreId);

        // Returns true if this call newly unlocked the entry.
        public static bool UnlockLore(Player player, string loreId)
            => AddToList(player, LoreUnlockedKey, loreId);

        public static IEnumerable<string> GetUnlockedLore(Player player)
            => ReadList(player, LoreUnlockedKey);

        // ── Lore stage (monotonic) ──────────────────────────────────────

        public static int GetLoreStage(Player player, string loreId)
            => ReadInt(player, LoreStagePrefix + loreId);

        // Returns true if the stored stage was advanced. No-op if `stage`
        // is <= the current stored value.
        public static bool AdvanceLoreStage(Player player, string loreId, int stage)
            => AdvanceInt(player, LoreStagePrefix + loreId, stage);

        // ── Boon tier (monotonic) ───────────────────────────────────────

        public static int GetBoonTier(Player player, string boonId)
            => ReadInt(player, BoonPrefix + boonId);

        // Returns true if the stored tier was upgraded.
        public static bool UpgradeBoonTier(Player player, string boonId, int tier)
            => AdvanceInt(player, BoonPrefix + boonId, tier);

        // ── One-time flags ──────────────────────────────────────────────

        public static bool HasFlag(Player player, string flag)
            => ContainsInList(player, FlagsKey, flag);

        // Returns true if the flag was newly set.
        public static bool SetFlag(Player player, string flag)
            => AddToList(player, FlagsKey, flag);

        // ── Completionist sets ──────────────────────────────────────────
        // Each completionist feat (e.g. bosses_defeated, biomes_entered)
        // stores a distinct-entry set under TSP_journal_set_<featId>.
        // Tier evaluation reads the count; entry-presence reads membership.

        // Returns true if this call newly added the entry to the set.
        public static bool AddCompletionistEntry(Player player, string featId, string entryId)
            => AddToList(player, CompletionistPrefix + featId, entryId);

        public static bool IsCompletionistEntryPresent(Player player, string featId, string entryId)
            => ContainsInList(player, CompletionistPrefix + featId, entryId);

        public static IEnumerable<string> GetCompletionistEntries(Player player, string featId)
            => ReadList(player, CompletionistPrefix + featId);

        public static int GetCompletionistCount(Player player, string featId)
        {
            int count = 0;
            foreach (string _ in ReadList(player, CompletionistPrefix + featId))
                count++;
            return count;
        }

        // ── Internals ───────────────────────────────────────────────────

        private static int ReadInt(Player player, string key)
        {
            if (player?.m_customData == null) return 0;
            if (!player.m_customData.TryGetValue(key, out string raw)) return 0;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        private static void WriteInt(Player player, string key, int value)
        {
            if (player?.m_customData == null) return;
            player.m_customData[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool AdvanceInt(Player player, string key, int target)
        {
            if (player?.m_customData == null) return false;
            int current = ReadInt(player, key);
            if (target <= current) return false;
            WriteInt(player, key, target);
            return true;
        }

        private static IEnumerable<string> ReadList(Player player, string key)
        {
            if (player?.m_customData == null) yield break;
            if (!player.m_customData.TryGetValue(key, out string raw) || string.IsNullOrEmpty(raw)) yield break;
            foreach (string item in raw.Split(','))
            {
                if (!string.IsNullOrEmpty(item)) yield return item;
            }
        }

        private static bool ContainsInList(Player player, string key, string item)
        {
            if (player?.m_customData == null || string.IsNullOrEmpty(item)) return false;
            if (!player.m_customData.TryGetValue(key, out string raw) || string.IsNullOrEmpty(raw)) return false;
            foreach (string existing in raw.Split(','))
            {
                if (existing == item) return true;
            }
            return false;
        }

        private static bool AddToList(Player player, string key, string item)
        {
            if (player?.m_customData == null || string.IsNullOrEmpty(item)) return false;
            player.m_customData.TryGetValue(key, out string raw);
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string existing in raw.Split(','))
                {
                    if (existing == item) return false;
                }
                player.m_customData[key] = raw + "," + item;
            }
            else
            {
                player.m_customData[key] = item;
            }
            return true;
        }
    }
}
