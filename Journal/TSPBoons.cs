namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // TSP's boon content — the catalog data, separated from the registry
    // framework. RegisterAll() called once from Plugin.Awake.
    public static class TSPBoons
    {
        public const string StoneKin = "stone_kin";

        public static void RegisterAll()
        {
            BoonRegistry.Register(new BoonDef(
                id: StoneKin,
                name: "Stone-Kin",
                gatingFeatIds: new[] { Feats.KinOnlyGolemKills, Feats.GolemUnarmedSurvived },
                durationByTier: new[] { 600f, 1200f, 1800f },   // 10 / 20 / 30 min
                applyBoon: ApplyStoneKin
            ));

            BoonSystem.RegisterRitual(StoneKinRitual.Tick, StoneKinRitual.ClearAll);
        }

        // Stubbed for E.3a — SE_StoneKin lands in E.3b and replaces this.
        private static void ApplyStoneKin(Player player, int tier)
        {
            Log.Info($"BoonSystem: [stub] would apply Stone-Kin tier {tier} to {player?.GetPlayerName()}");
        }
    }
}
