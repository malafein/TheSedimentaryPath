using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // TSP's boon content — the catalog data, separated from the registry
    // framework. RegisterAll() called once from Plugin.Awake.
    public static class TSPBoons
    {
        public const string StoneKin = "stone_kin";
        public const string Holdfast = "holdfast";

        private static readonly int StoneKinSEHash = "SE_StoneKin".GetStableHashCode();

        public static void RegisterAll()
        {
            BoonRegistry.Register(new BoonDef(
                id: StoneKin,
                name: "Stone-Kin",
                gatingFeatIds: new[] { Feats.KinOnlyGolemKills, Feats.GolemUnarmedSurvived },
                durationByTier: new[] { 600f, 1200f, 1800f },   // 10 / 20 / 30 min
                applyBoon: ApplyStoneKinDefault,
                description: "The Rock takes you as kin. While unarmored and bare-handed (or holding a kin-weapon), the stone's defenses are yours.",
                ritualText: "Kneel at a Mysterious Rock of worth and hold for five seconds.",
                effectsByTier: new[]
                {
                    "Resistant (50%) to Fire, Frost, Poison, Pierce, Slash.\nKnockback reduced to 70%.",
                    "Immune to Fire, Frost, Poison.\nResistant (50%) to Pierce, Slash.\nKnockback reduced to 50%.",
                    "Immune to Fire, Frost, Poison.\nResistant (50%) to Pierce, Slash.\nKnockback reduced to 25%.\nBare fists strike with the stone's weight.",
                }
            ));

            BoonSystem.RegisterRitual(StoneKinRitual.Tick, StoneKinRitual.ClearAll);

            BoonRegistry.Register(new BoonDef(
                id: Holdfast,
                name: "Holdfast",
                gatingFeatIds: new[] { Feats.AbomRootedKills, Feats.AbomStillSeconds },
                durationByTier: new[] { 600f, 1200f, 1800f },   // 10 / 20 / 30 min CAPS — the vigil fills them
                applyBoon: ApplyHoldfastDefault,
                description: "The Vine holds you as its own. While the vine is in hand (or its full raiment worn) and your feet stand on living ground, its gifts are yours.",
                ritualText: "Sit and watch among a well-watched grove. The ritual takes hold near half a minute of watching; the longer you watch, the longer the vine holds.",
                effectsByTier: new[]
                {
                    "Health regen ×1.5.\nResistant to Poison.\nThe Wet chill no longer weakens you.",
                    "Health regen ×2.\nImmune to Poison.\nMelee attackers that strike you may be snared.",
                    "Health regen ×2.\nImmune to Poison.\nAttackers are rooted instead of snared, held longer the more the grove was watched.",
                }
            ));

            BoonSystem.RegisterRitual(HoldfastRitual.Tick, HoldfastRitual.ClearAll);
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

        // Called from HoldfastRitual with the grove score and the vigil's
        // accrued duration. The score drives the tier-3 root-hold length
        // via the curve in SE_Holdfast.Initialize.
        public static void ApplyHoldfast(Player player, int tier, float groveScore, float durationSeconds)
        {
            if (player == null || tier <= 0 || durationSeconds <= 0f) return;

            SEMan seman = player.GetSEMan();
            if (seman == null) return;

            SE_Holdfast se = seman.GetStatusEffect(SE_Holdfast.Hash) as SE_Holdfast
                          ?? seman.AddStatusEffect(SE_Holdfast.Hash) as SE_Holdfast;

            if (se == null)
            {
                Log.Error("TSPBoons.ApplyHoldfast: SEMan did not return SE_Holdfast instance");
                return;
            }

            se.Initialize(tier, durationSeconds, groveScore);
            Log.Debug($"TSPBoons: Holdfast applied — tier={tier} duration={durationSeconds:0}s score={groveScore:0}");

            BoonSystem.PersistBoonTier(player, Holdfast, tier);
        }

        // Fallback adapter (dev console, non-ritual grants): full tier cap,
        // zero grove score — the tier-3 hold sits at its minimum duration.
        private static void ApplyHoldfastDefault(Player player, int tier)
        {
            BoonDef def = BoonRegistry.Get(Holdfast);
            float cap = (def != null && tier >= 1 && tier <= def.DurationByTier.Length)
                ? def.DurationByTier[tier - 1]
                : 0f;
            ApplyHoldfast(player, tier, 0f, cap);
        }
    }
}
