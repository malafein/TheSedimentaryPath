using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Items;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    // Turns the vanilla green vine (what VineGreenSeeds grows into) into the source of
    // Bindsinew — but only on TENDED vines (ones descended from a player-planted seed).
    // Wild green vines that spawn with the world stay inert.
    //
    // How it hangs together:
    //   • Repurpose (once, at ObjectDB.Awake): find the green vine prefab via the
    //     VineGreenSeeds → Plant → grown-Vine chain and repoint its Pickable at
    //     Bindsinew. Because m_itemPrefab lives on the prefab, every instance AND every
    //     vine-spread copy (Vine re-instantiates the same prefab) inherits it — and it
    //     doubles as our identity check (a vine whose pickable drops Bindsinew is ours),
    //     which survives Vine.Grow truncating spread-child names to 15 chars.
    //   • Tended flag: a ZDO int set by the owner. Unity runs Awake synchronously inside
    //     Instantiate, so a static "growth context" set by the Plant.Grow / Vine.Grow
    //     prefixes is live when the new vine's Awake (→ ConfigureInstance) runs:
    //       - grew from a Plant  → tended
    //       - spread from a vine → inherit the parent's tended flag
    //       - neither (world-gen / a reloaded vine) → whatever is already persisted (wild
    //         vines are never marked, so they read 0)
    //   • Gating: a green vine yields Bindsinew only when it is BOTH tended AND watched
    //     (the Vinery ritual — plant it, sit and watch it grow). "Watched" is a second
    //     ZDO flag that propagates the same way (set at Plant.Grow when the sapling had
    //     watchers; inherited by spread children), with a live fallback so watching a
    //     grown tended vine also qualifies it. The Pickable respawns only when its
    //     m_spawnCheck passes; we wrap that with "&& productive", so un-watched/wild vines
    //     never (re)spawn Bindsinew, and force them picked at Awake so they start inert.
    //     All MP-safe: the flags ride the synced ZDO; only the owner writes them.
    public static class BindsinewVine
    {
        // ZDO ints: 1 = true. Absent/0 = false.
        private static readonly int TendedKey  = "TSP_tended".GetStableHashCode();  // descended from a planted seed
        private static readonly int WatchedKey = "TSP_watched".GetStableHashCode(); // planted-and-watched lineage

        // Growth context, set by the Plant.Grow / Vine.Grow prefixes and read by the
        // new vine's Awake (which runs synchronously during their Instantiate call).
        public static bool s_growingFromPlant;
        public static bool s_growingFromPlantWatched;
        public static Vine s_spreadParent;

        public static GameObject GreenVinePrefab { get; private set; }

        // ── Repurpose (once, at init) ───────────────────────────────────────────

        // Finds the green vine prefab and repoints its Pickable at Bindsinew. Idempotent.
        public static void FindAndRepurpose()
        {
            if (GreenVinePrefab != null) return; // already done
            if (Plugin.BindsinewPrefab == null)
            {
                Log.Warn("BindsinewVine: Bindsinew prefab missing — skipping green-vine repurpose");
                return;
            }

            GameObject greenVine = FindGreenVinePrefab(out Plant sapling);
            if (greenVine == null)
            {
                Log.Warn("BindsinewVine: could not find the VineGreenSeeds grown-vine prefab — Bindsinew will not drop");
                return;
            }

            Pickable pk = greenVine.GetComponent<Pickable>();
            if (pk == null)
            {
                Log.Warn($"BindsinewVine: {greenVine.name} has no Pickable — cannot repurpose");
                return;
            }

            Vine vineComp = greenVine.GetComponent<Vine>();
            Log.Debug($"BindsinewVine: repurposing '{greenVine.name}' pickable (was {pk.m_itemPrefab?.name}, " +
                $"respawnMin={pk.m_respawnTimeMinutes}, maxBerries={(vineComp != null ? vineComp.m_maxBerriesWithinBlocker : -1)})");

            pk.m_itemPrefab   = Plugin.BindsinewPrefab;
            pk.m_amount       = 1;
            pk.m_overrideName = "";              // fall back to Bindsinew's name (fixes the "Vineberry" mislabel)
            if (pk.m_respawnTimeMinutes <= 0f)
                pk.m_respawnTimeMinutes = 30f;   // ensure a tended patch keeps yielding

            // VineGreen ships with m_maxBerriesWithinBlocker = 0 — that's the real reason
            // vanilla green vines never grow anything (CheckBerryBlocker can never pass, so
            // the pickable is force-picked forever). Re-enable it (vanilla berry vines use 2)
            // so a branched tended+watched vine can present a Bindsinew node. Our IsProductive
            // spawn-check still gates it, so wild/un-watched vines remain inert.
            if (vineComp != null && vineComp.m_maxBerriesWithinBlocker <= 0)
                vineComp.m_maxBerriesWithinBlocker = 2;

#if DEBUG
            // Debug testing levers: shrink the grow/respawn timers so the full
            // plant → watch → mature → harvest loop runs in seconds. New instances only.
            float respawnOverride = Plugin.DebugBindsinewRespawnMinutes?.Value ?? 0f;
            if (respawnOverride > 0f)
            {
                pk.m_respawnTimeMinutes = respawnOverride;
                Log.Info($"BindsinewVine [DEBUG]: Bindsinew respawn overridden to {respawnOverride} min");
            }
            float growOverride = Plugin.DebugVineGrowSeconds?.Value ?? 0f;
            if (growOverride > 0f && sapling != null)
            {
                sapling.m_growTime = growOverride;
                sapling.m_growTimeMax = growOverride;
                Log.Info($"BindsinewVine [DEBUG]: green-vine sapling grow time overridden to {growOverride}s");
            }
#endif

            GreenVinePrefab = greenVine;
            Log.Debug($"BindsinewVine: '{greenVine.name}' now yields Bindsinew (tended+watched vines only)");
            Log.Info("TSP: flora extras ready");
        }

        // VineGreenSeeds → the Plant piece (sapling) that consumes it → its grown Vine prefab.
        private static GameObject FindGreenVinePrefab(out Plant sapling)
        {
            sapling = null;
            GameObject seed = ObjectDB.instance?.GetItemPrefab("VineGreenSeeds");
            if (seed == null || ZNetScene.instance == null) return null;

            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                Plant plant = prefab.GetComponent<Plant>();
                if (plant == null || plant.m_grownPrefabs == null) continue;

                Piece piece = prefab.GetComponent<Piece>();
                if (piece == null || !RequiresSeed(piece, seed)) continue;

                foreach (GameObject grown in plant.m_grownPrefabs)
                {
                    if (grown != null && grown.GetComponent<Vine>() != null)
                    {
                        sapling = plant;
                        return grown;
                    }
                }
            }
            return null;
        }

        private static bool RequiresSeed(Piece piece, GameObject seed)
        {
            if (piece.m_resources == null) return false;
            foreach (Piece.Requirement req in piece.m_resources)
            {
                if (req?.m_resItem != null && req.m_resItem.gameObject == seed)
                    return true;
            }
            return false;
        }

        // ── Per-instance gating (from VineAwakePatch.Postfix) ───────────────────

        public static void ConfigureInstance(Vine vine)
        {
            if (Plugin.BindsinewPrefab == null || vine == null) return;

            Pickable pk = vine.GetComponent<Pickable>();
            if (pk == null || pk.m_itemPrefab != Plugin.BindsinewPrefab) return; // not our green vine

            // Re-enable the berry blocker on this instance too — instances spawned before
            // the prefab fix (or under an older build) still carry VineGreen's 0.
            if (vine.m_maxBerriesWithinBlocker <= 0)
                vine.m_maxBerriesWithinBlocker = 2;

            ZNetView nview = vine.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            // Decide & persist the tended/watched flags once, on the owner. Only true
            // values are written; wild/un-watched vines are left unmarked (read as 0).
            if (nview.IsOwner())
            {
                if (zdo.GetInt(TendedKey, 0) == 0)
                {
                    bool becomeTended = s_growingFromPlant
                        || (s_spreadParent != null && HasFlag(s_spreadParent, TendedKey));
                    if (becomeTended) zdo.Set(TendedKey, 1);
                }
                if (zdo.GetInt(WatchedKey, 0) == 0)
                {
                    bool becomeWatched = (s_growingFromPlant && s_growingFromPlantWatched)
                        || (s_spreadParent != null && HasFlag(s_spreadParent, WatchedKey));
                    if (becomeWatched) zdo.Set(WatchedKey, 1);
                }
            }

            // Gate respawn on productivity (preserving the vine's own berry-blocker check).
            Pickable.SpawnCheck prev = pk.m_spawnCheck;
            pk.m_spawnCheck = (Pickable p) => (prev == null || prev(p)) && IsProductive(nview);

            // Non-productive vines start inert (in case the pickable would otherwise show).
            if (nview.IsOwner())
            {
                bool productive = IsProductive(nview);
                if (!productive)
                    pk.SetPicked(picked: true);

                Log.Debug($"BindsinewVine: green vine {zdo.m_uid} — tended={zdo.GetInt(TendedKey, 0)}, " +
                    $"watched={zdo.GetInt(WatchedKey, 0)}, liveCredit={zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f):F1}, " +
                    $"productive={productive}");
            }
        }

        // Called from the vine watch-credit RPC handler: watching a tended vine marks it
        // watched, so it (and its future spread children) yield Bindsinew even if the
        // sapling itself was never watched. Owner-side; no-op on non-green/wild vines.
        public static void MarkWatchedIfTended(ZDO zdo)
        {
            if (zdo == null) return;
            if (zdo.GetInt(TendedKey, 0) == 1 && zdo.GetInt(WatchedKey, 0) == 0)
                zdo.Set(WatchedKey, 1);
        }

        // A green vine yields Bindsinew when tended AND (watched-flag OR any live watch
        // credit — so watching a grown tended vine qualifies it immediately).
        private static bool IsProductive(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return false;
            ZDO zdo = nview.GetZDO();
            if (zdo == null || zdo.GetInt(TendedKey, 0) != 1) return false;
            return zdo.GetInt(WatchedKey, 0) == 1
                || zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f) > 0f;
        }

        private static bool HasFlag(Vine vine, int key)
        {
            ZNetView nv = vine != null ? vine.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return false;
            ZDO zdo = nv.GetZDO();
            return zdo != null && zdo.GetInt(key, 0) == 1;
        }
    }
}
