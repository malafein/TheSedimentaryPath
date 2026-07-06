using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // The rockery weapons' on-hit marker effect. A thrown Kaldmörk hit carries no
    // other TSP signal — the HitData skill is the native Knives and the dagger is
    // consumed at throw time, so the right-hand check in
    // AchievementSystem.IsTSPRockeryWeaponHit can't see it either. The marker rides
    // HitData.m_statusEffectHash instead (same mechanism as the vinery weapons'
    // snare/root/tether), giving kill attribution a reliable signal on every hit.
    // Invisible by design: no icon, no name, no stat modifiers, sub-second TTL.
    public static class StoneStatusEffects
    {
        public const string MarkEffectName = "SE_StoneMark";

        public static SE_Stats Mark { get; private set; }

        // Idempotent: safe to call on every ObjectDB.Awake.
        public static void Build()
        {
            if (Mark != null) return;

            Mark = ScriptableObject.CreateInstance<SE_Stats>();
            Mark.name   = MarkEffectName;
            Mark.m_name = "";
            Mark.m_icon = null;
            Mark.m_ttl  = 0.1f;
        }
    }
}
