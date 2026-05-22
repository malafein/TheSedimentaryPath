using System.Collections.Generic;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // ── LoreEntry ────────────────────────────────────────────────────
    // A piece of journal lore. May have a single stage (one-shot unlock)
    // or multiple stages where each stage's condition reveals more text.
    // Stages are evaluated strictly sequentially — stage N can only
    // advance once stage N-1 is already the player's current stage.
    public class LoreEntry
    {
        public string                  Id     { get; }
        public string                  Title  { get; }
        public IReadOnlyList<LoreStage> Stages { get; }

        public LoreEntry(string id, string title, params LoreStage[] stages)
        {
            Id     = id;
            Title  = title;
            Stages = stages ?? new LoreStage[0];
        }
    }

    // ── LoreStage ────────────────────────────────────────────────────
    // One revelation in an entry. The condition gates whether this
    // stage unlocks; the text is shown in the journal detail view
    // when this is the highest unlocked stage for the entry.
    public class LoreStage
    {
        public string        Text      { get; }
        public LoreCondition Condition { get; }

        public LoreStage(string text, LoreCondition condition)
        {
            Text      = text;
            Condition = condition;
        }
    }

    // ── LoreCondition ────────────────────────────────────────────────
    // Abstract gate. Concrete subclasses cover the seven event shapes
    // Phase 1 supports. Evaluate(player) must be cheap — it's called
    // from event-driven dispatch in LoreChecker and from a one-shot
    // evaluation pass on Player.OnSpawned.
    public abstract class LoreCondition
    {
        public abstract bool Evaluate(Player player);
    }

    // ── Concrete conditions ──────────────────────────────────────────

    // Fires once when the named one-time flag is set on the player.
    // Use for cult-mythic milestones (first ritual performed, etc.)
    // that don't naturally fall out of an existing feat counter.
    public class FirstTimeFlag : LoreCondition
    {
        public string FlagId { get; }
        public FirstTimeFlag(string flagId) { FlagId = flagId; }
        public override bool Evaluate(Player player) =>
            JournalData.HasFlag(player, FlagId);
    }

    // Fires when a tiered-counter feat reaches the given counter value.
    // Use Threshold == 1 for "first time the event fires" gates.
    public class FeatThreshold : LoreCondition
    {
        public string FeatId    { get; }
        public int    Threshold { get; }
        public FeatThreshold(string featId, int threshold)
        {
            FeatId    = featId;
            Threshold = threshold;
        }
        public override bool Evaluate(Player player) =>
            JournalData.GetFeat(player, FeatId) >= Threshold;
    }

    // Fires when a completionist-shaped feat (e.g. bosses_defeated,
    // biomes_entered) contains at least the given number of distinct
    // entries.
    public class FeatCompletionistCount : LoreCondition
    {
        public string FeatId { get; }
        public int    Count  { get; }
        public FeatCompletionistCount(string featId, int count)
        {
            FeatId = featId;
            Count  = count;
        }
        public override bool Evaluate(Player player) =>
            JournalData.GetCompletionistCount(player, FeatId) >= Count;
    }

    // Fires when a skill reaches the given level. Reads
    // Player.GetSkillLevel directly rather than the mirror feat so
    // off-by-one across the skill/feat sync window doesn't matter.
    public class SkillLevel : LoreCondition
    {
        public ValheimSkills.SkillType Skill { get; }
        public int              Level { get; }
        public SkillLevel(ValheimSkills.SkillType skill, int level)
        {
            Skill = skill;
            Level = level;
        }
        public override bool Evaluate(Player player) =>
            player != null && player.GetSkillLevel(Skill) >= Level;
    }

    // Fires when the player has visited the given biome. Reads
    // Player.IsBiomeKnown — source of truth — so the condition stays
    // correct even if a biome got into the known set outside our
    // tracking.
    public class BiomeEntered : LoreCondition
    {
        public Heightmap.Biome Biome { get; }
        public BiomeEntered(Heightmap.Biome biome) { Biome = biome; }
        public override bool Evaluate(Player player) =>
            player != null && player.IsBiomeKnown(Biome);
    }

    // Fires when the player has unlocked the given recipe. The name is
    // the *shared* name (e.g. "$item_swordbronze" or a piece's
    // m_name) — not the prefab name. That's what Player.m_knownRecipes
    // stores.
    public class RecipeUnlocked : LoreCondition
    {
        public string RecipeSharedName { get; }
        public RecipeUnlocked(string recipeSharedName) { RecipeSharedName = recipeSharedName; }
        public override bool Evaluate(Player player) =>
            player != null && player.IsRecipeKnown(RecipeSharedName);
    }

    // Fires when the named boon reaches the given tier.
    public class BoonTierReached : LoreCondition
    {
        public string BoonId { get; }
        public int    Tier   { get; }
        public BoonTierReached(string boonId, int tier)
        {
            BoonId = boonId;
            Tier   = tier;
        }
        public override bool Evaluate(Player player) =>
            JournalData.GetBoonTier(player, BoonId) >= Tier;
    }
}
