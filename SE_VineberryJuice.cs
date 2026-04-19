using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public class SE_VineberryJuice : SE_DrunkMead
    {
        public const float Duration      = 1800f; // 30 minutes
        public const float StaminaBonus  = 40f;
        public const float EitrBonus     = 30f;

        public override Skills.SkillType AssociatedSkill => VinerySkill.SkillType;

        // Fraction of the buff remaining, using the same power curve as vanilla food.
        public float GetDecayFactor()
        {
            if (m_ttl <= 0f) return 0f;
            float fraction = Mathf.Clamp01(1f - m_time / m_ttl);
            return Mathf.Pow(fraction, 0.3f);
        }
    }
}
