namespace malafein.Valheim.TheSedimentaryPath
{
    // Implemented by any weapon that supports a toggled secondary-attack stance.
    // Register instances in Plugin.StanceWeapons (keyed by $item_* name) so the
    // hotkey and equip patches can route without knowing weapon names directly.
    public interface IStanceWeapon
    {
        void ToggleStance();
        void ApplyStance();
    }
}
