using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    // Persistent buff shown while a stance-toggle weapon is equipped.
    // Icon and HUD text are refreshed by IStanceWeapon.ShowCurrentStance().
    // Auto-expires when the player no longer has a stance weapon in hand.
    public class SE_WeaponStance : StatusEffect
    {
        private string _stanceDisplayName = "";

        // Called by IStanceWeapon implementations on equip and stance change.
        public void Refresh(string stanceDisplayName, Sprite icon)
        {
            _stanceDisplayName = stanceDisplayName;
            m_icon  = icon;
            m_time  = 0f;
        }

        // Shown as the small badge text on the buff icon in the HUD.
        public override string GetIconText() => _stanceDisplayName;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!(m_character is Player player)) return;

            string itemName = player.GetCurrentWeapon()?.m_shared?.m_name;
            if (itemName == null || !Plugin.StanceWeapons.ContainsKey(itemName))
                m_time = m_ttl; // force IsDone() → true
        }
    }
}
