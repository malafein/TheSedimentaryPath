using System.Collections.Generic;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // Holdfast status effect — the Vinery cult's boon, Stone-Kin's
    // counterpart. Growth (regen, poison wards, wet negation) and grasp
    // (snare/root retaliation against melee attackers).
    //
    // Lifecycle mirrors SE_StoneKin: Initialize(tier, duration, groveScore)
    // is called by TSPBoons.ApplyHoldfast right after SEMan adds the cloned
    // instance. Each UpdateStatusEffect tick, HoldfastPredicate is checked
    // and the effects swap between the tier values (doctrine held) and
    // neutral (doctrine broken), with HUD messages on the flips.
    //
    // Effects ladder:
    //   Tier 1 — nourished:       regen ×1.5; Poison Resistant; Wet's
    //            penalties negated (rain and swamp water nourish the vine).
    //   Tier 2 — the grasp wakes: regen ×2; Poison Immune; melee attackers
    //            that strike you may be snared — a fully blocked hit still
    //            counts (the vine grabs what reaches for you).
    //   Tier 3 — the vine holds fast: retaliation upgrades snare → full
    //            root; the grove's watch-time score scales the hold's
    //            DURATION (not its chance) on the KinFist-style asymptotic
    //            curve. Regen holds at ×2 — tier 3 is the grasp tier.
    public class SE_Holdfast : SE_Stats
    {
        public const string EffectName = "SE_Holdfast";
        public static readonly int Hash = EffectName.GetStableHashCode();

        // ── Tuning ───────────────────────────────────────────────────────

        // Grasping retaliation: flat chance per melee hit received, tiers
        // 2+. No Vinery-skill term (skill scales what your weapons do; tier
        // and score scale what the boon does) — and the rate is tuned
        // knowing a blocking player farms procs.
        public const float RetaliationChance = 0.22f;

        // Retaliation hold durations: min + (max-min) × score/(score+halfGain),
        // asymptotic at max — "the longer you watched, the harder the vine
        // holds." One law for both tiers, in the ritual's WATCHED-VINE
        // UNITS (see HoldfastRitual.ComputeGroveScore). The tier-2 snare
        // curves up from its authored 4s; the tier-3 root reaches for 10s,
        // and its proc also lays the snare for the root's duration plus a
        // tail — the hard hold releases into a lingering slow, the vine
        // letting go reluctantly. Curve points at 6 / 12 / 30 / 60 units —
        // snare: 5 / 5.5 / 6.1 / 6.5s; root: 5.3 / 6.5 / 8 / 8.8s.
        public const float SnareHoldMinSeconds = 4f;
        public const float SnareHoldMaxSeconds = 7f;
        public const float RootHoldMinSeconds  = 3f;
        public const float RootHoldMaxSeconds  = 10f;
        public const float HoldHalfGainScore   = 12f;
        public const float SnareTailSeconds    = 2f;

        private static readonly int WetHash = "Wet".GetStableHashCode();

        // Dormant-state sentinel. Shared empty list so we don't allocate
        // each predicate flip.
        private static readonly List<HitData.DamageModPair> Empty
            = new List<HitData.DamageModPair>(0);

        public int Tier { get; private set; }

        private List<HitData.DamageModPair> _tierMods;
        private float _tierRegenMult = 1f;
        private float _rootHoldSeconds  = RootHoldMinSeconds;
        private float _snareHoldSeconds = SnareHoldMinSeconds;
        private bool _initialized;
        private bool _firstTick = true;
        private bool _wasActive;

        // Wet-negation bookkeeping: the Wet SE instance we neutralized and
        // its stashed original values, restored when doctrine breaks (or
        // when the instance is gone and a fresh Wet appears).
        private SE_Stats _neutralizedWet;
        private float _wetOrigHealthRegen;
        private float _wetOrigStaminaRegen;
        private List<HitData.DamageModPair> _wetOrigMods;

        // Called from TSPBoons.ApplyHoldfast right after SEMan adds the
        // cloned SE instance. Duration is the ritual's accrued grant (vigil
        // held → duration, capped per tier), not a fixed per-tier value.
        public void Initialize(int tier, float durationSeconds, float groveScore)
        {
            Tier           = tier;
            m_ttl          = durationSeconds;
            // Reset elapsed time so re-performing the ritual refreshes the
            // duration (SEMan reuses the existing instance on a re-ritual).
            m_time         = 0f;
            _tierMods      = BuildModsForTier(tier);
            _tierRegenMult = tier >= 2 ? 2f : 1.5f;
            float gain = groveScore / (groveScore + HoldHalfGainScore);
            _rootHoldSeconds = RootHoldMinSeconds
                + (RootHoldMaxSeconds - RootHoldMinSeconds) * gain;
            _snareHoldSeconds = SnareHoldMinSeconds
                + (SnareHoldMaxSeconds - SnareHoldMinSeconds) * gain;
            _initialized   = true;
            // Re-baseline active/dormant tracking so a re-ritual doesn't fire
            // a spurious transition message next tick.
            _firstTick     = true;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!_initialized) return;
            if (!(m_character is Player player) || player != Player.m_localPlayer) return;

            bool active = HoldfastPredicate.IsActive(player);

            // Swap the contributions so vanilla aggregation only sees them
            // while doctrine is held.
            m_mods = active ? _tierMods : Empty;
            m_healthRegenMultiplier = active ? _tierRegenMult : 1f;

            UpdateWetNegation(player, active);

            // Suppress the transition message on first tick — the ritual
            // completion already showed "the vine holds fast to you".
            if (_firstTick)
            {
                _wasActive = active;
                _firstTick = false;
                return;
            }

            if (active != _wasActive)
            {
                Notify.Center(active
                    ? "the vine holds you again"
                    : "the vine has let you go",
                    0f);
                Log.Debug($"SE_Holdfast: doctrine {(active ? "held" : "broken")}");
                _wasActive = active;
            }
        }

        // Grasping retaliation, called from CharacterDamageRetaliationPatch
        // when a melee hit reaches the local player (blocked or not). Tiers
        // 2+, doctrine held, flat chance; both holds scale with the grove
        // score. Tier 2 snares; tier 3 roots AND lays the snare for the
        // root's duration plus a tail, so the hard hold releases into a
        // lingering slow.
        public void TryRetaliate(Character attacker)
        {
            if (Tier < 2 || !_wasActive || attacker == null) return;
            if (Random.value >= RetaliationChance) return;

            if (Tier >= 3)
            {
                VineHoldRpc.Apply(attacker, _rootHoldSeconds, _rootHoldSeconds + SnareTailSeconds);
                Log.Debug($"SE_Holdfast: retaliation — root {_rootHoldSeconds:0.0}s + snare tail on {attacker.name}");
            }
            else
            {
                VineHoldRpc.Apply(attacker, 0f, _snareHoldSeconds);
                Log.Debug($"SE_Holdfast: retaliation — snare {_snareHoldSeconds:0.0}s on {attacker.name}");
            }
        }

        // While doctrine is held, the Wet debuff's PENALTIES stop applying:
        // regen multipliers back to 1, Weak/VeryWeak damage mods stripped.
        // Wet's fire-resist upside is kept — being soaked still damps fire.
        // The icon stays in the HUD; the vine negates, it does not dry you.
        private void UpdateWetNegation(Player player, bool active)
        {
            SE_Stats wet = player.GetSEMan()?.GetStatusEffect(WetHash) as SE_Stats;

            // The instance we neutralized expired or was replaced — our
            // stashed values died with it.
            if (_neutralizedWet != null && !ReferenceEquals(wet, _neutralizedWet))
            {
                _neutralizedWet = null;
                _wetOrigMods    = null;
            }

            if (active)
            {
                if (wet == null || _neutralizedWet != null) return;

                _wetOrigHealthRegen  = wet.m_healthRegenMultiplier;
                _wetOrigStaminaRegen = wet.m_staminaRegenMultiplier;
                _wetOrigMods         = wet.m_mods;

                wet.m_healthRegenMultiplier  = 1f;
                wet.m_staminaRegenMultiplier = 1f;
                wet.m_mods = FilterOutWeaknesses(wet.m_mods);
                _neutralizedWet = wet;
                Log.Debug("SE_Holdfast: wet penalties negated");
            }
            else if (_neutralizedWet != null)
            {
                _neutralizedWet.m_healthRegenMultiplier  = _wetOrigHealthRegen;
                _neutralizedWet.m_staminaRegenMultiplier = _wetOrigStaminaRegen;
                _neutralizedWet.m_mods = _wetOrigMods;
                _neutralizedWet = null;
                _wetOrigMods    = null;
                Log.Debug("SE_Holdfast: wet penalties restored");
            }
        }

        private static List<HitData.DamageModPair> FilterOutWeaknesses(List<HitData.DamageModPair> mods)
        {
            var kept = new List<HitData.DamageModPair>(mods != null ? mods.Count : 0);
            if (mods == null) return kept;
            foreach (HitData.DamageModPair pair in mods)
            {
                if (pair.m_modifier == HitData.DamageModifier.Weak
                    || pair.m_modifier == HitData.DamageModifier.VeryWeak)
                    continue;
                kept.Add(pair);
            }
            return kept;
        }

        private static List<HitData.DamageModPair> BuildModsForTier(int tier)
        {
            var mods = new List<HitData.DamageModPair>(1);
            if (tier < 1) return mods;

            mods.Add(new HitData.DamageModPair
            {
                m_type     = HitData.DamageType.Poison,
                m_modifier = tier >= 2
                    ? HitData.DamageModifier.Immune
                    : HitData.DamageModifier.Resistant,
            });
            return mods;
        }
    }
}
