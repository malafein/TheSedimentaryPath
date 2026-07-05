using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // The RootSpear's Cast reel. Applied to the target by the thrown vine projectile
    // (routed via SharedData.m_attackStatusEffect → HitData.m_statusEffectHash).
    //
    // Runs on the target's owner (the server, for creatures), which is the correct
    // authority to move the target's rigidbody. Character.Damage calls SetAttacker()
    // on us with the thrower, so we know who to reel toward. The rope visual is the
    // projectile's own LineConnect (projectile → owner) — we only do the pull.
    //
    // A brief, sharp yank (short m_ttl), reeling the victim IN toward the attacker.
    // Uses Utils.Pull with useForce:false → ForceMode.VelocityChange (mass-independent),
    // so it reliably reels ordinary mobs. The earlier Impulse (mass-scaled) version was
    // too punishing — heavier mobs like Draugr wouldn't move at all. Bosses are still
    // held immune below. (A gentle mass factor could re-add "heavier creatures resist"
    // for the truly heavy without making common mobs unpullable.)
    //
    // The vine identity is snare-on-every-hit, so the tether also lays the snare on
    // its victim (Cast routes the tether, not the snare, as the on-hit effect).
    public class SE_VineTether : StatusEffect
    {
        public float m_pullSpeed      = 40f;   // mass-independent (VelocityChange): a quick, reliable reel
        public float m_reelToDistance = 2.5f;  // reel in toward roughly this distance
        public float m_smoothDistance = 1f;
        public float m_forcePower     = 1f;    // near-linear falloff — stays strong most of the way
        public float m_maxDistance    = 30f;
        // "Heaviness" resistance: rb.mass is near-uniform (~50) across Valheim creatures,
        // so it can't tell a troll from a Draugr. Max health is the usable proxy — small
        // mobs (Neck ~15, Draugr ~100) reel at full speed; tough ones (Troll ~600) resist.
        // Bosses are immune above.
        public float m_referenceHealth = 150f;   // creatures at/below this reel at full speed
        public float m_minFactor       = 0.15f;  // toughest still creep in a little
        public float m_resistPower     = 1.5f;   // >1: tougher creatures fall off faster
        public float m_maxUpSpeed      = 6f;     // reel up to you if elevated, but don't launch the target

        private Character m_attacker;
        private bool m_broken;

        public override void Setup(Character character)
        {
            base.Setup(character);
            // Snare rides along on the Cast hit (poison comes from m_damages).
            m_character?.GetSEMan()?.AddStatusEffect(VineStatusEffects.SnareHash, resetTime: true);
        }

        public override void SetAttacker(Character attacker)
        {
            m_attacker = attacker;

            // Silently fail on bosses (immune) or out-of-range hooks — no reel.
            if (m_attacker == null || m_character == null || m_character.IsBoss())
            {
                m_broken = true;
                return;
            }
            float dist = Vector3.Distance(m_attacker.transform.position, m_character.transform.position);
            if (dist > m_maxDistance)
                m_broken = true;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (m_attacker == null || m_character == null) return;

            // Don't fight ship decks or an attached/mounted state.
            if (m_character.GetStandingOnShip() != null || m_character.IsAttached()) return;

            Rigidbody rb = m_character.GetComponent<Rigidbody>();
            if (rb == null) return;

            // Mass-independent VelocityChange (useForce:false) for a quick, reliable reel,
            // scaled down gently for heavy creatures so weight still matters. noUpForce:false
            // so the target is reeled to your actual position — including up, if you're
            // elevated (vanilla used true only because it reels serpents across water).
            float health = m_character.GetMaxHealth();
            float ratio  = m_referenceHealth / Mathf.Max(health, 1f);
            float factor = Mathf.Clamp(Mathf.Pow(ratio, m_resistPower), m_minFactor, 1f);
            Utils.Pull(rb, m_attacker.transform.position, m_reelToDistance, m_pullSpeed * factor,
                force: 1f, m_smoothDistance, noUpForce: false, useForce: false, m_forcePower);

            // Reel up to you if you're elevated, but cap the lift so the target isn't launched.
            Vector3 v = rb.velocity;
            if (v.y > m_maxUpSpeed) { v.y = m_maxUpSpeed; rb.velocity = v; }
        }

        public override bool IsDone()
        {
            return base.IsDone() || m_broken;
        }
    }
}
