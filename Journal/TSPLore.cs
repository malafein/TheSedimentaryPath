using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // TSP's lore content — the entry catalog, separate from the framework
    // so adding a new entry touches one file. Parallel to TSPBoons.
    //
    // Phase 1 starter set: seven entries spanning the basic Stone Path
    // beats, one Ferment intro, the first biome crossing, and the
    // three-stage Stone-Kin arc tied to the boon tiers.
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
        public const string StoneKin             = "stone_kin";

        // The Vine Path — parallel to the Stone Path firsts + apprentice,
        // wired to the vine feats already credited by the Vinery patches.
        public const string FirstVinePlanted     = "first_vine_planted";
        public const string FirstVineMatured     = "first_vine_matured";
        public const string VineryApprentice     = "vinery_apprentice";

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
            LoreRegistry.Register(new LoreEntry(
                id: FirstMysteriousRock,
                title: "One Among the Stones",
                new LoreStage(
                    text: "Among the field-stones, one sat heavier than its size, and did not look away. The others are only stone. This one waits. Carry it to where it can stand, and it will keep watch over you.",
                    condition: new FeatThreshold(Feats.MysteriousRockFound, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstRockPlaced,
                title: "The First Watcher",
                new LoreStage(
                    text: "One stone, set standing. One more pair of eyes for the Rock.",
                    condition: new FeatThreshold(Feats.MysteriousRocksPlaced, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstBlackstoneBrew,
                title: "The First Sip",
                new LoreStage(
                    text: "The black draught goes down heavier than water. Something settles in the gut, and stays.",
                    condition: new FirstTimeFlag(FirstBlackstoneBrew)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstStoneWeapon,
                title: "The First Knapping",
                new LoreStage(
                    text: "Stone struck on stone, and a tool came from the striking. The oldest making, returned to.",
                    condition: new FeatThreshold(Feats.StoneWeaponsCrafted, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstStoneKill,
                title: "The Stone Answers",
                new LoreStage(
                    text: "You struck, and the stone answered. The first time of many. There will be no last.",
                    condition: new FeatThreshold(Feats.StoneKills, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: RockeryApprentice,
                title: "The Stone Grows Near",
                new LoreStage(
                    text: "Where they lie close, the body now knows before the eye does.",
                    condition: new SkillLevel(RockerySkill.SkillType, 25)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstVinePlanted,
                title: "The First Sowing",
                new LoreStage(
                    text: "A seed pressed into the earth, and the waiting begins. The vine keeps its own slow counsel.",
                    condition: new FeatThreshold(Feats.VinesPlanted, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: FirstVineMatured,
                title: "The Vine Answers",
                new LoreStage(
                    text: "You watched, and the vine answered — leaf by leaf, in its own time. Patience is the only tending it asks.",
                    condition: new FeatThreshold(Feats.VinesGrown, 1)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: VineryApprentice,
                title: "The Vine Grows Near",
                new LoreStage(
                    text: "Where the green things gather close, the body now feels them before the eye can find them.",
                    condition: new SkillLevel(VinerySkill.SkillType, 25)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: PeakPilgrimage,
                title: "The Throat of the World",
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

            LoreRegistry.Register(new LoreEntry(
                id: BlackForestFirst,
                title: "The Dark Trees",
                new LoreStage(
                    text: "Past the soft grass, the trunks stand close, and the light bleeds through them slowly.",
                    condition: new BiomeEntered(Heightmap.Biome.BlackForest)
                )
            ));

            LoreRegistry.Register(new LoreEntry(
                id: StoneKin,
                title: "Stone Knows Its Own",
                new LoreStage(
                    text: "Knelt at the standing stone, and the body did not refuse it. Something has taken hold.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 1)
                ),
                new LoreStage(
                    text: "What once cut, no longer cuts so deep. Stone-skin, in fragments.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 2)
                ),
                new LoreStage(
                    text: "The hands have become the stone. What they strike, the stone strikes.",
                    condition: new BoonTierReached(TSPBoons.StoneKin, 3)
                )
            ));
        }
    }
}
