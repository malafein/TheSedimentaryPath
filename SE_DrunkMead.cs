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

                // Puking purges all OTHER drunk mead effects
                // TODO: Extend to future meads that don't necessarily cause puke, but should still be purged by puking
                var effects = player.GetSEMan().GetStatusEffects();
                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    var se = effects[i];
                    if (se is SE_DrunkMead && se.name != this.name)
                    {
                        player.GetSEMan().RemoveStatusEffect(se.name.GetStableHashCode(), quiet: false);
                    }
                }
            }
        }

        // Lateral stumble is applied in Patches.DrunkStumblePatch as an input
        // modification on Player.SetControls, so the walk animation plays and
        // the character rotates to face the stumble direction.
    }
}
