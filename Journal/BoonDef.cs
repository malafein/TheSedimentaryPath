using System;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Definition of a boon — what it's called, what feats gate its tiers,
    // how long it lasts at each tier, and how to apply it to a player.
    //
    // A boon's tier is the minimum tier across all its gating feats. So
    // Stone-Kin at tier N requires both kin_only_golem_kills AND
    // golem_unarmed_survived to have reached tier N. If any gating feat is
    // at tier 0, the boon is locked.
    public class BoonDef
    {
        public string Id { get; }
        public string Name { get; }
        public string[] GatingFeatIds { get; }
        public float[] DurationByTier { get; }
        public Action<Player, int> ApplyBoon { get; }

        public BoonDef(string id, string name, string[] gatingFeatIds,
                       float[] durationByTier, Action<Player, int> applyBoon)
        {
            Id              = id;
            Name            = name;
            GatingFeatIds   = gatingFeatIds   ?? new string[0];
            DurationByTier  = durationByTier  ?? new float[0];
            ApplyBoon       = applyBoon;
        }

        public int MaxTier => DurationByTier.Length;
    }
}
