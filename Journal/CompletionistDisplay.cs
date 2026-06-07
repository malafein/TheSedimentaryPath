using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Maps a completionist set's stored entry IDs to player-readable names for
    // the Feats tab detail view. Only the entries the player has actually
    // completed are ever passed in — unfinished members stay hidden so the
    // journal never spoils what's left to find.
    //
    // Entry-ID formats differ per feat (see the recording patches):
    //   biomes / distant-lands  → ((int)Heightmap.Biome).ToString()
    //   bosses / felled creatures → creature prefab name
    //   traders                 → Trader.m_name (often a $localization token)
    //   runestones / brews      → opaque hashes / not yet keyed → no name
    //
    // Returns null when an entry can't be made into a meaningful name; the
    // caller omits those rows (and hides the expand affordance entirely if a
    // feat yields no nameable members).
    public static class CompletionistDisplay
    {
        public static string ResolveMemberName(string featId, string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;

            switch (featId)
            {
                case Feats.BiomesEntered:
                case Feats.RocksInDistantLands:
                    return int.TryParse(entryId, out int biomeVal)
                        ? BiomeName((Heightmap.Biome)biomeVal)
                        : null;

                case Feats.BossesDefeated:
                case Feats.BossesStoneOnly:
                case Feats.BossesUnarmored:
                case Feats.DrunkPilgrimBosses:
                case Feats.StoneOnlyCreaturesFelled:
                    return CreatureName(entryId);

                case Feats.TradersVisited:
                    return Localize(entryId);

                case Feats.DistinctRockTypes:
                    return PickableItemName(entryId);

                default:
                    // runestones_read (position-hashed text), brews_variety
                    // (deferred) — nothing nameable to show.
                    return null;
            }
        }

        // Prefab name → localized creature display name, falling back to a
        // prettified prefab if the prefab/Character can't be resolved.
        private static string CreatureName(string prefab)
        {
            GameObject go = ZNetScene.instance?.GetPrefab(prefab);
            Character ch = go != null ? go.GetComponent<Character>() : null;
            string token = ch != null ? ch.m_name : null;
            if (!string.IsNullOrEmpty(token))
                return Localize(token);
            return Prettify(prefab);
        }

        // Pickable prefab name → the localized name of the item it yields
        // (e.g. "Pickable_Flint" → "Flint"), falling back to a prettified prefab.
        private static string PickableItemName(string pickablePrefab)
        {
            GameObject go = ZNetScene.instance?.GetPrefab(pickablePrefab);
            Pickable p = go != null ? go.GetComponent<Pickable>() : null;
            ItemDrop drop = p?.m_itemPrefab != null ? p.m_itemPrefab.GetComponent<ItemDrop>() : null;
            string token = drop?.m_itemData?.m_shared?.m_name;
            if (!string.IsNullOrEmpty(token))
                return Localize(token);
            return Prettify(pickablePrefab.Replace("Pickable_", ""));
        }

        private static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            if (Localization.instance != null)
                return Localization.instance.Localize(token);
            return token;
        }

        // Turn a prefab-ish identifier into something legible as a last resort:
        // strip a leading subtype tag and split on common separators.
        private static string Prettify(string raw)
        {
            string s = raw.Replace('_', ' ').Replace('-', ' ').Trim();
            return string.IsNullOrEmpty(s) ? raw : s;
        }

        // Vanilla builds biome display names as "$biome_<lowercasename>" and
        // localizes them (Player.AddKnownBiome / ShowBiomeFoundMsg), so reuse
        // that token form rather than hardcoding English strings.
        private static string BiomeName(Heightmap.Biome biome)
        {
            if (biome == Heightmap.Biome.None) return null;
            return Localize("$biome_" + biome.ToString().ToLower());
        }
    }
}
