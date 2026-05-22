namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Canonical list of TSP rockery weapons and the matching helpers
    // that distinguish them from non-TSP items. The four kin weapons:
    //
    //   HeftyStone   — heavy throwing stone (melee + ranged)
    //   SmoothStone  — flint-flat skipping stone (melee + ranged)
    //   Kaldmork     — obsidian frost dagger (split skill: Knives × Rockery)
    //   Dokkblad     — obsidian frost sword (split skill: Swords × Rockery)
    //
    // Two name domains exist for any item in Valheim:
    //
    //   • Prefab name — "HeftyStone". Used by ZNetScene's named prefab
    //     map, by ZDOVars.s_rightItem (a stable int hash of the prefab
    //     name), and as the file/asset identity in ObjectDB.
    //   • Localization key — "$item_heftystone". Used as
    //     m_shared.m_name, the value passed into XP/skill systems and
    //     compared by item-display-name callers.
    //
    // Both domains have a "is this a TSP rockery weapon?" question.
    // Routing both through this class keeps the canonical list in one
    // place — adding or removing a weapon touches only TSPRockeryWeapons.
    public static class TSPRockeryWeapons
    {
        // ── Prefab names ────────────────────────────────────────────────
        public const string HeftyStonePrefab  = "HeftyStone";
        public const string SmoothStonePrefab = "SmoothStone";
        public const string KaldmorkPrefab    = "Kaldmork";
        public const string DokkbladPrefab    = "Dokkblad";

        // ── Localization keys (m_shared.m_name) ─────────────────────────
        public const string HeftyStoneName  = "$item_heftystone";
        public const string SmoothStoneName = "$item_smoothstone";
        public const string KaldmorkName    = "$item_kaldmork";
        public const string DokkbladName    = "$item_dokkblad";

        // ── Precomputed prefab-name hashes for fast per-frame comparison ─
        public static readonly int HeftyStoneHash  = HeftyStonePrefab.GetStableHashCode();
        public static readonly int SmoothStoneHash = SmoothStonePrefab.GetStableHashCode();
        public static readonly int KaldmorkHash    = KaldmorkPrefab.GetStableHashCode();
        public static readonly int DokkbladHash    = DokkbladPrefab.GetStableHashCode();

        // True if the given prefab-name hash matches any of the four
        // (e.g. ZDOVars.s_rightItem, or itemData.m_dropPrefab.name hash).
        public static bool MatchesHash(int prefabHash)
            => prefabHash == HeftyStoneHash
            || prefabHash == SmoothStoneHash
            || prefabHash == KaldmorkHash
            || prefabHash == DokkbladHash;

        // True if the given $item_* localization key matches any of the
        // four (e.g. ItemData.m_shared.m_name).
        public static bool MatchesItemName(string itemName)
            => itemName == HeftyStoneName
            || itemName == SmoothStoneName
            || itemName == KaldmorkName
            || itemName == DokkbladName;
    }
}
