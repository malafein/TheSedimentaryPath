using System;
using System.Collections.Generic;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Generic boon orchestrator. Knows about boons in the abstract — how
    // to compute tier from gating feats and how to apply a boon to a
    // player — but does NOT know about specific activation mechanics
    // (kneel, ritual, shrine, etc.).
    //
    // Each boon's activation logic lives in its own ritual class (e.g.
    // StoneKinRitual for the kneel-at-rock-shrine boon). Rituals register
    // their per-frame Tick here at startup; BoonSystem just dispatches.
    // When a ritual's conditions are met, it calls BoonSystem.GrantBoon
    // to apply the actual effect.
    //
    // This keeps the boon framework open to other activation patterns —
    // a future Vinery boon can have its own VineryBoonRitual with
    // completely different conditions and the rest of the system doesn't
    // care.
    public static class BoonSystem
    {
        // ── Ritual dispatch ──────────────────────────────────────────────

        private struct RitualHandlers
        {
            public Action<Player, float> Tick;
            public Action ClearAll;
        }

        private static readonly List<RitualHandlers> _rituals = new List<RitualHandlers>();

        // Called once per ritual at startup (from TSPBoons.RegisterAll).
        public static void RegisterRitual(Action<Player, float> tick, Action clearAll = null)
        {
            if (tick == null) return;
            _rituals.Add(new RitualHandlers { Tick = tick, ClearAll = clearAll });
        }

        // Per-frame entry, called from PlayerUpdatePatch on the local player.
        // Dispatches to each registered ritual; the ritual is responsible
        // for its own early-exit when not active.
        public static void Tick(Player player, float dt)
        {
            for (int i = 0; i < _rituals.Count; i++)
                _rituals[i].Tick?.Invoke(player, dt);
        }

        // Drop ritual state on world unload (called from ZNetScenePatch).
        public static void ClearAll()
        {
            for (int i = 0; i < _rituals.Count; i++)
                _rituals[i].ClearAll?.Invoke();
        }

        // ── Boon application (called by rituals when they fire) ──────────

        // Computes the player's current tier for a boon. 0 = locked
        // (at least one gating feat is below its first threshold).
        public static int ComputeBoonTier(Player player, BoonDef def)
        {
            if (def == null || def.GatingFeatIds.Length == 0) return 0;
            int min = int.MaxValue;
            for (int i = 0; i < def.GatingFeatIds.Length; i++)
            {
                int t = FeatTracker.GetCurrentTier(player, def.GatingFeatIds[i]);
                if (t < min) min = t;
            }
            return Mathf.Min(min, def.MaxTier);
        }

        // Grant a boon to the player. Computes the tier internally; if the
        // tier is 0, calls onLocked (so the ritual can show its own
        // not-yet-worthy message). Otherwise dispatches to the boon's
        // ApplyBoon callback.
        //
        // Returns the granted tier (0 if locked).
        public static int GrantBoon(Player player, string boonId, Action onLocked = null)
        {
            BoonDef def = BoonRegistry.Get(boonId);
            if (def == null)
            {
                Log.Warn($"BoonSystem.GrantBoon: unknown boon '{boonId}'");
                return 0;
            }

            int tier = ComputeBoonTier(player, def);
            if (tier == 0)
            {
                onLocked?.Invoke();
                return 0;
            }

            Log.Info($"BoonSystem: granting '{boonId}' tier={tier} to {player?.GetPlayerName()}");
            def.ApplyBoon?.Invoke(player, tier);
            return tier;
        }
    }
}
