using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
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

        public override Skills.SkillType AssociatedSkill => RockerySkill.SkillType;

        public override void ModifyJump(Vector3 baseJump, ref Vector3 jump)
        {
            // Heavier belly — reduce jump height without affecting horizontal momentum
            jump.y *= 0.75f;
        }

        public override void ModifyFallDamage(float baseDamage, ref float damage)
        {
            // The stone does not float — feather cloaks offer no protection
            damage = baseDamage;
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
