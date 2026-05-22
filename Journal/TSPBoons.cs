using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // TSP's boon content — the catalog data, separated from the registry
    // framework. RegisterAll() called once from Plugin.Awake.
    public static class TSPBoons
    {
        public const string StoneKin = "stone_kin";

        private static readonly int StoneKinSEHash = "SE_StoneKin".GetStableHashCode();

        public static void RegisterAll()
        {
            BoonRegistry.Register(new BoonDef(
                id: StoneKin,
                name: "Stone-Kin",
                gatingFeatIds: new[] { Feats.KinOnlyGolemKills, Feats.GolemUnarmedSurvived },
                durationByTier: new[] { 600f, 1200f, 1800f },   // 10 / 20 / 30 min
                applyBoon: ApplyStoneKinDefault
            ));

            BoonSystem.RegisterRitual(StoneKinRitual.Tick, StoneKinRitual.ClearAll);
        }

        // Called from StoneKinRitual with the cached shrine score at
        // ritual time. The score drives tier-3 KinFist damage via the
        // curve in SE_StoneKin.Initialize.
        public static void ApplyStoneKin(Player player, int tier, int shrineScore)
        {
            if (player == null || tier <= 0) return;

            BoonDef def = BoonRegistry.Get(StoneKin);
            if (def == null)
            {
                Log.Error("TSPBoons.ApplyStoneKin: Stone-Kin boon def not registered");
                return;
            }

            float duration = (tier <= def.DurationByTier.Length)
                ? def.DurationByTier[tier - 1]
                : 0f;

            SEMan seman = player.GetSEMan();
            if (seman == null) return;

            // Re-ritual at a different tier needs a fresh init, so consult
            // an existing instance if present; otherwise add a new one.
            // SEMan adds a clone of the ObjectDB-registered template.
            SE_StoneKin se = seman.GetStatusEffect(StoneKinSEHash) as SE_StoneKin
                          ?? seman.AddStatusEffect(StoneKinSEHash) as SE_StoneKin;

            if (se == null)
            {
                Log.Error("TSPBoons.ApplyStoneKin: SEMan did not return SE_StoneKin instance");
                return;
            }

            se.Initialize(tier, duration, shrineScore);
            Log.Info($"TSPBoons: Stone-Kin applied — tier={tier} duration={duration}s score={shrineScore}");

            // Ritual path bypasses BoonSystem.GrantBoon; record the
            // tier and let lore react here.
            BoonSystem.PersistBoonTier(player, StoneKin, tier);
        }

        // BoonDef.ApplyBoon delegate adapter — fallback for callers that
        // grant the boon outside a ritual (e.g. dev console, future
        // non-ritual gifts). Uses a placeholder score; tier-3 KinFist
        // damage will be minimum-curve based on the score (currently 0).
        private static void ApplyStoneKinDefault(Player player, int tier)
            => ApplyStoneKin(player, tier, 0);
    }
}
