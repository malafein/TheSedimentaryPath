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
    //   traders                 → Trader.m_name ($npc_* token), or the prefab
    //                             name when m_name ships empty (BogWitch)
    //   vinery weapons          → $item_* localization token
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
                // Int-keyed enums — a generic resolver can't tell a biome
                // value from an opaque hash.
                case Feats.BiomesEntered:
                case Feats.RocksInDistantLands:
                    return int.TryParse(entryId, out int biomeVal)
                        ? BiomeName((Heightmap.Biome)biomeVal)
                        : null;

                // Mixed $-token / prefab entries with an npc-token fallback.
                case Feats.TradersVisited:
                    return TraderName(entryId);

                // Position-hashed text — deliberately nothing nameable.
                case Feats.RunestonesRead:
                    return null;

                // Everything else resolves generically, so a new
                // completionist set whose entries are $-tokens or prefab
                // names (creatures, pickables, items) gets its dropdown
                // for free — no registration to forget here.
                default:
                    return GenericName(entryId);
            }
        }

        // Generic entry-ID resolution chain: localization token, then the
        // prefab interpreted as creature / pickable / item, then a
        // prettified prefab name. Purely-numeric IDs (opaque hashes, e.g.
        // brews_variety's deferred keying) stay hidden — a number is never
        // a meaningful member name.
        private static string GenericName(string entryId)
        {
            if (entryId.StartsWith("$"))
                return Localize(entryId);
            if (long.TryParse(entryId, out _))
                return null;

            GameObject go = ZNetScene.instance?.GetPrefab(entryId);
            if (go != null)
            {
                Character ch = go.GetComponent<Character>();
                if (!string.IsNullOrEmpty(ch?.m_name))
                    return Localize(ch.m_name);

                Pickable p = go.GetComponent<Pickable>();
                ItemDrop pickDrop = p?.m_itemPrefab != null ? p.m_itemPrefab.GetComponent<ItemDrop>() : null;
                string pickToken = pickDrop?.m_itemData?.m_shared?.m_name;
                if (!string.IsNullOrEmpty(pickToken))
                    return Localize(pickToken);

                ItemDrop drop = go.GetComponent<ItemDrop>();
                string itemToken = drop?.m_itemData?.m_shared?.m_name;
                if (!string.IsNullOrEmpty(itemToken))
                    return Localize(itemToken);
            }

            return Prettify(entryId.Replace("Pickable_", ""));
        }

        // Trader entry → localized display name. $-token entries localize
        // directly; prefab-name entries (empty Trader.m_name, e.g. BogWitch)
        // try the vanilla "$npc_<prefab>" token convention — Localize returns
        // "[token]" when the key is unknown, in which case fall back to the
        // prettified prefab name (covers modded traders with no npc token).
        private static string TraderName(string entryId)
        {
            if (entryId.StartsWith("$"))
                return Localize(entryId);

            string localized = Localize("$npc_" + entryId.ToLowerInvariant());
            if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("["))
                return localized;
            return Prettify(entryId);
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
