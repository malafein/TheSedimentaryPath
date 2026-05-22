using System.Collections.Generic;
using malafein.Valheim.TheSedimentaryPath.Items;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // Stone-Kin status effect.
    //
    // Lifecycle:
    //   Initialize(tier, duration) is called by TSPBoons.ApplyStoneKin
    //   right after SEMan adds the cloned instance. Sets m_ttl and the
    //   tier field; builds the per-tier damage-mod list once and caches
    //   it. Each UpdateStatusEffect tick, the StoneKinPredicate is
    //   checked and m_mods is swapped between the tier mods (doctrine
    //   active) and an empty list (doctrine broken). HUD transition
    //   messages fire on the active⇄dormant flips.
    //
    // Doctrine gating by m_mods swap (rather than overriding a hook on
    // SE_Stats) survives base-class refactors and keeps the per-tick
    // cost trivial: one predicate evaluation + one reference
    // assignment + (rarely) a Notify call.
    //
    // Damage ladder:
    //   Tier 1: Resistant (50%) to Fire/Frost/Poison/Pierce/Slash
    //   Tier 2: Immune to Fire/Frost/Poison; Resistant Pierce/Slash
    //   Tier 3: same damage profile as Tier 2 (the tier-3 unarmed buff
    //           lives in E.3c via Humanoid.GetCurrentWeapon postfix).
    public class SE_StoneKin : SE_Stats
    {
        // Dormant-state sentinel. Shared empty list so we don't allocate
        // each predicate flip.
        private static readonly List<HitData.DamageModPair> Empty
            = new List<HitData.DamageModPair>(0);

        public int Tier { get; private set; }

        private List<HitData.DamageModPair> _tierMods;
        private bool _initialized;
        private bool _firstTick = true;
        private bool _wasActive;

        // Called from TSPBoons.ApplyStoneKin right after SEMan adds the
        // cloned SE instance. Sets per-instance tier, duration, and
        // (at tier 3) the KinFist's blunt damage derived from the
        // shrine score that funded this ritual.
        //
        // Score curve: damage = 150 × score / (score + 82.5).
        // Asymptotic at 150; ~40 at score 30, ~106 at score 200,
        // ~138 at score 1000.
        public void Initialize(int tier, float durationSeconds, int shrineScore)
        {
            Tier         = tier;
            m_ttl        = durationSeconds;
            _tierMods    = BuildModsForTier(tier);
            _initialized = true;

            if (tier >= 3 && KinFist.IsReady)
            {
                float damage = 150f * shrineScore / (shrineScore + 82.5f);
                KinFist.SetBluntDamage(damage);
            }
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!_initialized) return;
            if (!(m_character is Player player) || player != Player.m_localPlayer) return;

            bool active = StoneKinPredicate.IsActive(player);

            // Swap m_mods so vanilla SE_Stats aggregation only sees our
            // contribution when doctrine is held.
            m_mods = active ? _tierMods : Empty;

            // Suppress the transition message on first tick — the ritual
            // completion already showed "the stone takes you as kin";
            // an "again" message immediately afterward would read as a
            // duplicate.
            if (_firstTick)
            {
                _wasActive = active;
                _firstTick = false;
                return;
            }

            if (active != _wasActive)
            {
                Notify.Center(active
                    ? "the Rock knows you again"
                    : "the Rock no longer knows you as kin",
                    0f);
                _wasActive = active;
            }
        }

        private static List<HitData.DamageModPair> BuildModsForTier(int tier)
        {
            var mods = new List<HitData.DamageModPair>(5);
            if (tier < 1) return mods;

            // Tier 1: elementals are Resistant. Tier 2+: elementals become Immune.
            // Pierce/Slash stay Resistant across all tiers.
            HitData.DamageModifier elemental = tier >= 2
                ? HitData.DamageModifier.Immune
                : HitData.DamageModifier.Resistant;

            mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Fire,   m_modifier = elemental });
            mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Frost,  m_modifier = elemental });
            mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Poison, m_modifier = elemental });
            mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant });
            mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Slash,  m_modifier = HitData.DamageModifier.Resistant });

            return mods;
        }
    }
}
