using System.Collections.Generic;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    public class SE_Drunk : StatusEffect
    {
        public class DrunkInstance
        {
            public float RemainingTime;
            public float OriginalDuration;
            public float ToleranceMultiplier;
        }

        public List<DrunkInstance> Instances = new List<DrunkInstance>();

        public void AddDrunkInstance(float duration, float toleranceMultiplier)
        {
            Instances.Add(new DrunkInstance {
                RemainingTime = duration,
                OriginalDuration = duration,
                ToleranceMultiplier = toleranceMultiplier
            });
            UpdateTTL();
        }

        private void UpdateTTL()
        {
            float maxRemaining = 0f;
            foreach (var inst in Instances)
            {
                if (inst.RemainingTime > maxRemaining)
                    maxRemaining = inst.RemainingTime;
            }
            m_ttl = m_time + maxRemaining;
            if (Instances.Count == 0) m_ttl = m_time; // force remove if empty
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            
            bool changed = false;
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                Instances[i].RemainingTime -= dt;
                if (Instances[i].RemainingTime <= 0f)
                {
                    Instances.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed) UpdateTTL();
        }

        public void ReduceAllTimers(float amount)
        {
            bool changed = false;
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                Instances[i].RemainingTime -= amount;
                if (Instances[i].RemainingTime <= 0f)
                {
                    Instances.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed || Instances.Count > 0) UpdateTTL();
        }

        public float GetTotalMultiplier()
        {
            float total = 0f;
            foreach (var inst in Instances)
            {
                float timeDecay = Mathf.Clamp01(inst.RemainingTime / inst.OriginalDuration);
                total += timeDecay * inst.ToleranceMultiplier;
            }
            return total;
        }
    }
}
