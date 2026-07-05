namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Canonical list of TSP vinery weapons — the Vinery-skill counterparts to the
    // rockery kin weapons (see TSPRockeryWeapons). Two weapons, both stance-toggle:
    //
    //   RootAtgeir — 2H vine polearm, the "field" weapon (pure AoE control)
    //       • Sweep  (default): native atgeir 360° spin  → poison + snare
    //       • Furrow (stance B): rooting slam            → poison + hard root
    //   RootSpear  — 1H vine spear, the "line" weapon (single-target grapple)
    //       • Cast   (default): throw + reel the enemy TO you (spear returns)
    //       • Vault  (stance B): throw + pull YOURSELF to the enemy (spear returns)
    //
    // Every hit on both weapons applies poison (in m_damages) and a snare (movement
    // slow). Furrow upgrades the snare to a near-immobilizing root; the spear's Cast
    // reels via SE_VineTether. Routing the per-swing on-hit effect lives in
    // HumanoidStartAttackPatch (m_attackStatusEffect is weapon-wide).
    //
    // As with the rockery set, two name domains exist per item — the prefab name
    // (ZNetScene / ObjectDB identity, stable int hash) and the localization key
    // (m_shared.m_name). Both are centralized here.
    public static class TSPVineryWeapons
    {
        // ── Prefab names ────────────────────────────────────────────────
        public const string RootAtgeirPrefab = "RootAtgeir";
        public const string RootSpearPrefab  = "RootSpear";

        // ── Localization keys (m_shared.m_name) ─────────────────────────
        public const string RootAtgeirName = "$item_rootatgeir";
        public const string RootSpearName  = "$item_rootspear";

        // ── Precomputed prefab-name hashes for fast per-frame comparison ─
        public static readonly int RootAtgeirHash = RootAtgeirPrefab.GetStableHashCode();
        public static readonly int RootSpearHash  = RootSpearPrefab.GetStableHashCode();

        // True if the given prefab-name hash matches either vinery weapon.
        public static bool MatchesHash(int prefabHash)
            => prefabHash == RootAtgeirHash
            || prefabHash == RootSpearHash;

        // True if the given $item_* localization key matches either vinery weapon.
        public static bool MatchesItemName(string itemName)
            => itemName == RootAtgeirName
            || itemName == RootSpearName;
    }
}
