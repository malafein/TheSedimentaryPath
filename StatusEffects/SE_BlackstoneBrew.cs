using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // Status effect applied when the player drinks Blackstone Brew.
    // Does not occupy a food slot. Clears existing food via SE_Puke,
    // then provides a decaying +25 health / +25 stamina bonus and 2 hp/tick
    // regen over 25 minutes. Reduces jump height as a negative trade-off.
    public class SE_BlackstoneBrew : SE_DrunkMead
    {
        public const float Duration      = 1500f; // 25 minutes
        public const float HealthBonus   = 40f;
        public const float StaminaBonus  = 10f;

        public override ValheimSkills.SkillType AssociatedSkill => RockerySkill.SkillType;

        public override void Setup(Character character)
        {
            base.Setup(character);
            m_fallDamageModifier = -0.5f; // Halve fall damage
            m_noiseModifier = 0.8f;       // 80% noise penalty

            if (character is Player player && player == Player.m_localPlayer)
                JournalData.SetFlag(player, TSPLore.FirstBlackstoneBrew);
        }

        public override void ModifyJump(Vector3 baseJump, ref Vector3 jump)
        {
            // Heavier belly — reduce jump height without affecting horizontal momentum
            jump.y *= 0.75f;
        }

        public override void ModifyWalkVelocity(ref Vector3 vel)
        {
            base.ModifyWalkVelocity(ref vel);
            if (vel.y < 0f && m_character != null && !m_character.IsOnGround())
            {
                vel.y *= 1.2f;
            }
        }

        // Fraction of the buff remaining, using the same power curve as vanilla food.
        public float GetDecayFactor()
        {
            if (m_ttl <= 0f) return 0f;
            float fraction = Mathf.Clamp01(1f - m_time / m_ttl);
            return Mathf.Pow(fraction, 0.3f);
        }
    }
}
