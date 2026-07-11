using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // TSP's lore content — the entry catalog, separate from the framework
    // so adding a new entry touches one file. Parallel to TSPBoons.
    //
    // Entries are registered in narrative order within each LoreCategory:
    // the Lore tab groups by category and renders each section in
    // registration order, so the order below IS the order the journal
    // tells each journey in.
    //
    // first_troll is deliberately deferred until the troll boulder
    // weapon ships — the lore depends on that feature for the body text
    // to land.
    public static class TSPLore
    {
        public const string FirstMysteriousRock  = "first_mysterious_rock";
        public const string FirstRockPlaced      = "first_rock_placed";
        public const string FirstBlackstoneBrew  = "first_blackstone_brew";
        public const string FirstStoneWeapon     = "first_stone_weapon";
        public const string FirstStoneKill       = "first_stone_kill";
        public const string RockeryApprentice    = "rockery_apprentice";
        public const string BlackForestFirst     = "black_forest_first";
        public const string GolemAvatar          = "golem_avatar";
        public const string StoneKin             = "stone_kin";

        // The Vine Path — parallel to the Stone Path firsts + apprentice,
        // wired to the vine feats already credited by the Vinery patches.
        public const string FirstVinePlanted     = "first_vine_planted";
        public const string FirstVineMatured     = "first_vine_matured";
        public const string VineryApprentice     = "vinery_apprentice";
        public const string FirstBindsinew       = "first_bindsinew";
        public const string FirstVineWeapon      = "first_vine_weapon";
        public const string FirstVineKill        = "first_vine_kill";
        public const string AbominationAvatar    = "abomination_avatar";
        public const string Holdfast             = "holdfast";

        // The peak pilgrimage — a two-stage entry. The hint stage unlocks when
        // the player pets a Watcher and it leans them toward the world summit
        // (PetMysteriousRockPatch sets PeakHintFlag); the arrival stage unlocks
        // when they actually stand atop it (PeakReached feat).
        public const string PeakPilgrimage       = "peak_pilgrimage";
        public const string PeakHintFlag         = "peak_hint";

        // Flag IDs consumed by FirstTimeFlag conditions live in a
        // different customData list than entry IDs (TSP_journal_flags
        // vs TSP_journal_lore_unlocked), so reusing the entry-id string
        // as the flag id is safe and makes the entry-flag relationship
        // explicit. Patch sites call JournalData.SetFlag(player, id) —
        // notification dispatch is folded into SetFlag.

        public static void RegisterAll()
        {
            // ── The Stone Path ───────────────────────────────────────────
            // Find the Rock → set it standing → knap → first kill →
            // the skill milestone → the pilgrimage → the boon.

            LoreRegistry.Register(new LoreEntry(
                id: FirstMysteriousRock,
                title: "One Among the Stones",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "Among the field-stones, one sat heavier than its size, and did not look away. The others are only stone. This one waits. Carry it to where it can stand, and it will keep watch over you.",
                    condition: new FeatThreshold(Feats.MysteriousRockFound, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstRockPlaced,
                title: "The First Watcher",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "One stone, set standing. One more pair of eyes for the Rock.",
                    condition: new FeatThreshold(Feats.MysteriousRocksPlaced, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstStoneWeapon,
                title: "The First Knapping",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "Stone struck on stone, and a tool came from the striking. The oldest making, returned to.",
                    condition: new FeatThreshold(Feats.StoneWeaponsCrafted, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstStoneKill,
                title: "The Stone Answers",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "You struck, and the stone answered. The first time of many. There will be no last.",
                    condition: new FeatThreshold(Feats.StoneKills, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: RockeryApprentice,
                title: "The Stone Grows Near",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "Where they lie close, the body now knows before the eye does.",
                    condition: new SkillLevel(RockerySkill.SkillType, 25)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: PeakPilgrimage,
                title: "The Throat of the World",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "When the hand rests on the Watcher, the weight shifts — straining toward the high places, where the land reaches nearest the sky. It would be carried there.",
                    condition: new FirstTimeFlag(PeakHintFlag)
                ),
                new LoreStage(
                    text: "You climbed until the land gave out beneath the sky, and nothing stood higher — only thin cold air, and the long fall of the world below you.",
                    condition: new FeatThreshold(Feats.PeakReached, 1),
                    mode: LoreStageMode.Append
                ),
                new LoreStage(
                    text: "And when at last a Watcher was set upon that highest stone, the weight left your hands. It rests easy now, at the Throat of the World, and keeps its watch over all that lies below.",
                    condition: new FeatThreshold(Feats.WatcherAtPeak, 1),
                    mode: LoreStageMode.Append
                )
            ));

            // Trial breadcrumb — points the devoted at the golem trials.
            // Gated on the first stone-only felling (the practice the
            // aggressive trial extends) plus knowing the golems' biome.
            LoreRegistry.Register(new LoreEntry(
                id: GolemAvatar,
                title: "The Sleeping Brothers",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "In the high cold places, some stones are not stones. They rise when troubled, and walk — the Rock's eldest kin, vast and slow to anger. What is proven against lesser things is not yet proven; the stone weighs its own against its own. A brother felled by stone alone. Bare before a brother's wrath, and enduring it.",
                    condition: new AllOf(
                        new FeatCompletionistCount(Feats.StoneOnlyCreaturesFelled, 1),
                        new BiomeEntered(Heightmap.Biome.Mountain)
                    )
                )
            ));

            // Pre-ritual tease as stage 0 (unlocks when both trials reach
            // tier 1, i.e. the ritual would grant); the ritual stages
            // Append below it so the entry accretes as a record. Existing
            // saves mid-entry self-heal on the EvaluateAll spawn pass —
            // the shifted stage index re-advances off the persisted boon
            // tier (one-time re-announce accepted).
            LoreRegistry.Register(new LoreEntry(
                id: StoneKin,
                title: "Stone Knows Its Own",
                category: LoreCategory.StonePath,
                new LoreStage(
                    text: "Both proofs stand. The brothers have felt your stone, your empty hands, your patience. Nothing more is asked aloud. But before a Watcher set in a place of worth, the body grows heavy — heavy as the thing it honors.",
                    condition: new AllOf(
                        new FeatThreshold(Feats.KinOnlyGolemKills, Tier1(Feats.KinOnlyGolemKills)),
                        new FeatThreshold(Feats.GolemUnarmedSurvived, Tier1(Feats.GolemUnarmedSurvived))
                    )
                ),
                new LoreStage(
                    text: "Knelt at the standing stone, and the body did not refuse it. Something has taken hold.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 1),
                    mode: LoreStageMode.Append
                ),
                new LoreStage(
                    text: "What once cut, no longer cuts so deep. Stone-skin, in fragments.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 2),
                    mode: LoreStageMode.Append
                ),
                new LoreStage(
                    text: "The hands have become the stone. What they strike, the stone strikes.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 3),
                    mode: LoreStageMode.Append
                )
            ));

            // ── The Vine Path ────────────────────────────────────────────
            // Sow → watch it answer → the thread → the twining →
            // first kill → the skill milestone → the boon arc.

            LoreRegistry.Register(new LoreEntry(
                id: FirstVinePlanted,
                title: "The First Sowing",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "A seed pressed into the earth, and the waiting begins. The vine keeps its own slow counsel.",
                    condition: new FeatThreshold(Feats.VinesPlanted, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstVineMatured,
                title: "The Vine Answers",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "You watched, and the vine answered — leaf by leaf, in its own time. Patience is the only tending it asks.",
                    condition: new FeatThreshold(Feats.VinesGrown, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstBindsinew,
                title: "The Binding Thread",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "The watched vine gives up its thread. It coils to the hand, unwilling to lie still.",
                    condition: new FirstTimeFlag(FirstBindsinew)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstVineWeapon,
                title: "The First Twining",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "Thread wound over thread, and a weapon came from the winding. It has not stopped being a vine.",
                    condition: new FeatCompletionistCount(Feats.VineWeaponsCrafted, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstVineKill,
                title: "The Vine Holds Fast",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "It was held, and it did not get away. The vine keeps what it catches.",
                    condition: new FeatThreshold(Feats.VineWeaponKills, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: VineryApprentice,
                title: "The Vine Grows Near",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "Where the green things gather close, the body now feels them before the eye can find them.",
                    condition: new SkillLevel(VinerySkill.SkillType, 25)
                )
            ));

            // Trial breadcrumb — points the devoted at the Abomination
            // trials. Gated on the first felling-in-your-grasp (the
            // practice the aggressive trial extends) plus knowing the
            // Abomination's biome.
            LoreRegistry.Register(new LoreEntry(
                id: AbominationAvatar,
                title: "The Root That Walks",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "In the drowned lands, a thing of root and rot uproots itself and walks. The Vine knows it for its own — eldest, and unruly. The coils in your hands pull toward it: what has held lesser things would hold this too, even as it falls. And in its shadow, the feet remember the way of roots.",
                    condition: new AllOf(
                        new FeatCompletionistCount(Feats.RootedCreaturesFelled, 1),
                        new BiomeEntered(Heightmap.Biome.Swamp)
                    )
                )
            ));

            // Pre-ritual tease + the boon's own arc, mirroring stone_kin.
            LoreRegistry.Register(new LoreEntry(
                id: Holdfast,
                title: "The Vine Knows Its Own",
                category: LoreCategory.VinePath,
                new LoreStage(
                    text: "The walking root has known your grasp, and your stillness, and the vine has marked both. Nothing more is asked aloud. But vines long watched remember their watcher — and to the one who keeps the old vigil, the watching is returned.",
                    condition: new AllOf(
                        new FeatThreshold(Feats.AbomRootedKills, Tier1(Feats.AbomRootedKills)),
                        new FeatThreshold(Feats.AbomStillSeconds, Tier1(Feats.AbomStillSeconds))
                    )
                ),
                new LoreStage(
                    text: "Sat among the watched vines and kept their vigil, and was kept in turn. Something has taken root.",
                    condition: new BoonTierReached(TSPBoons.Holdfast, 1),
                    mode: LoreStageMode.Append
                ),
                new LoreStage(
                    text: "What reaches for you is reached for in turn. The vine does not ask leave to hold.",
                    condition: new BoonTierReached(TSPBoons.Holdfast, 2),
                    mode: LoreStageMode.Append
                ),
                new LoreStage(
                    text: "The hold has deepened. What the vine takes now, it keeps — and lets go as rooted things let go, reluctantly.",
                    condition: new BoonTierReached(TSPBoons.Holdfast, 3),
                    mode: LoreStageMode.Append
                )
            ));

            // ── The Ferment ──────────────────────────────────────────────
            // Grows with the Grausten / Basic Stone brews when they ship.

            LoreRegistry.Register(new LoreEntry(
                id: FirstBlackstoneBrew,
                title: "The First Sip",
                category: LoreCategory.Ferment,
                new LoreStage(
                    text: "The black draught goes down heavier than water. Something settles in the gut, and stays.",
                    condition: new FirstTimeFlag(FirstBlackstoneBrew)
                )
            ));

            // ── The Wider World ──────────────────────────────────────────
            // Biome strays. The Dark Trees is a dormant hook — troll lore
            // can give it a next stage later, at which point the
            // open-thread marker lights it up on its own.

            LoreRegistry.Register(new LoreEntry(
                id: BlackForestFirst,
                title: "The Dark Trees",
                category: LoreCategory.WiderWorld,
                new LoreStage(
                    text: "Past the soft grass, the trunks stand close, and the light bleeds through them slowly.",
                    condition: new BiomeEntered(Heightmap.Biome.BlackForest)
                )
            ));
        }

        // Tier-1 threshold of a trial feat, read from the feat catalog so
        // the pre-ritual teases track the trial tuning. Safe here:
        // Plugin.Awake forces FeatRegistry before TSPLore.RegisterAll.
        private static int Tier1(string featId)
        {
            FeatDef def = FeatRegistry.Get(featId);
            return (def != null && def.Thresholds.Length > 0) ? def.Thresholds[0] : 1;
        }
    }
}
