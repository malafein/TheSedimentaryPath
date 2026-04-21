using System.Collections.Generic;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public abstract class SE_DrunkMead : SE_Stats
    {
        // Subclasses define which skill governs their puke tolerance
        public abstract Skills.SkillType AssociatedSkill { get; }

        public float CurrentTime => m_time;

        public override void Setup(Character character)
        {
            base.Setup(character);

            if (character is not Player player) return;

            float skillLevel = player.GetSkillLevel(AssociatedSkill);

            // Skill 80+ grants immunity to puking
            if (skillLevel < 80f)
            {
                // Trigger the vanilla puke effect.
                var sePuke = ObjectDB.instance?.m_StatusEffects.Find(se => se is SE_Puke);
                if (sePuke != null)
                {
                    var pukeInst = player.GetSEMan().AddStatusEffect(sePuke, resetTime: true);
                    if (pukeInst != null)
                    {
                        var field = HarmonyLib.AccessTools.Field(pukeInst.GetType(), "m_tickInterval");
                        if (field != null)
                        {
                            field.SetValue(pukeInst, Mathf.Lerp(3.0f, 0.1f, skillLevel / 100f));
                        }
                    }
                }
                else
                {
                    ZLog.LogWarning($"[TheSedimentaryPath] {name}: SE_Puke not found in ObjectDB");
                }
            }

            // Apply Drunk Effect
            float tolerance = Mathf.Lerp(2.0f, 0.1f, skillLevel / 100f);
            float drunkDuration = 600f; // 10 minutes default drunk duration

            var seman = player.GetSEMan();
            int hash = "SE_Drunk".GetStableHashCode();
            var seDrunk = seman.GetStatusEffect(hash) as SE_Drunk;
            
            if (seDrunk == null)
            {
                seDrunk = ScriptableObject.CreateInstance<SE_Drunk>();
                seDrunk.name = "SE_Drunk";
                seDrunk.m_name = "Drunk";
                seDrunk.m_icon = this.m_icon;
                seman.AddStatusEffect(seDrunk);
                seDrunk = seman.GetStatusEffect(hash) as SE_Drunk; // Get the added instance
            }
            
            if (seDrunk != null)
            {
                seDrunk.AddDrunkInstance(drunkDuration, tolerance);
            }
        }
    }
}
