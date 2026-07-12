using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Handles proximity-toggle hotkeys in a Player.Update prefix so that:
    // 1. Input is only consumed when TakeInput() allows it (no firing through menus/chat).
    // 2. ZInput.ResetButtonStatus runs before the game's own Update logic reads those buttons,
    //    preventing the default R/V actions from also firing.
    [HarmonyPatch(typeof(Player), "Update")]
    public static class HotkeyInputPatch
    {
        private static readonly Dictionary<KeyCode, List<string>> _keyToZInputButtons
            = new Dictionary<KeyCode, List<string>>();
        private static MethodInfo _keyCodeToPathMethod;
        private static MethodInfo _takeInputMethod;

        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            if (_takeInputMethod == null)
                _takeInputMethod = AccessTools.Method(typeof(Player), "TakeInput");
            if (!(bool)_takeInputMethod.Invoke(__instance, null))
            {
                HandleJournalOverInventory();
                return;
            }

            HandleHotkey(Plugin.ToggleRockeryProximity, () =>
            {
                Plugin.RockeryProximityAlert.Value = !Plugin.RockeryProximityAlert.Value;
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center,
                    $"Rockery sense: {(Plugin.RockeryProximityAlert.Value ? "Enabled" : "Disabled")}");
            });

            HandleHotkey(Plugin.ToggleVineryProximity, () =>
            {
                Plugin.VineryProximityAlert.Value = !Plugin.VineryProximityAlert.Value;
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center,
                    $"Vinery sense: {(Plugin.VineryProximityAlert.Value ? "Enabled" : "Disabled")}");
            });

            HandleHotkey(Plugin.ToggleWeaponStance, () =>
            {
                string itemName = __instance.GetCurrentWeapon()?.m_shared?.m_name;
                if (itemName != null && Plugin.StanceWeapons.TryGetValue(itemName, out IStanceWeapon weapon))
                    weapon.ToggleStance();
            });

            // Open the journal. Re-press to close + ESC close are owned
            // by JournalUI itself — once the panel is up, TakeInput goes
            // false and this Prefix early-exits, so the journal needs to
            // handle its own close path.
            HandleHotkey(Plugin.JournalHotkey, () =>
            {
                if (!JournalUI.IsOpen) JournalUI.Open();
            });
        }

        // Inventory ⇄ Journal open symmetry, journal side: the journal key
        // works over the OPEN INVENTORY specifically — the inventory yields
        // and the journal opens (the inverse swap lives in
        // InventoryGuiShowJournalPatch). Every other TakeInput-false state
        // stays inert: chat, console, menu, and the open journal itself
        // (whose re-press close is owned by JournalUI.Update).
        private static void HandleJournalOverInventory()
        {
            // IsVisible() is only patched true while the journal is open, so
            // with the journal closed a true here is the real inventory.
            if (JournalUI.IsOpen || !InventoryGui.IsVisible()) return;
            if (Console.IsVisible() || Menu.IsVisible() || TextInput.IsVisible()) return;
            if (Chat.instance != null && Chat.instance.HasFocus()) return;

            HandleHotkey(Plugin.JournalHotkey, () =>
            {
                InventoryGui.instance.Hide();
                JournalUI.Open();
            });
        }

        private static void HandleHotkey(ConfigEntry<KeyboardShortcut> config, System.Action onFired)
        {
            var shortcut = config.Value;
            if (shortcut.MainKey == KeyCode.None) return;
            if (!Input.GetKeyDown(shortcut.MainKey)) return;

            foreach (var mod in shortcut.Modifiers)
                if (!Input.GetKey(mod)) return;

            // Suppress any ZInput buttons bound to this key so the game's default
            // action for that key doesn't also fire.
            foreach (var buttonName in GetZInputButtonsForKey(shortcut.MainKey))
                ZInput.ResetButtonStatus(buttonName);

            onFired();
        }

        // Returns all ZInput button names whose current binding maps to the given KeyCode.
        // Result is cached per key for the session (re-queried if not yet seen).
        private static List<string> GetZInputButtonsForKey(KeyCode key)
        {
            if (_keyToZInputButtons.TryGetValue(key, out var cached)) return cached;

            var result = new List<string>();
            _keyToZInputButtons[key] = result;

            if (ZInput.instance == null) return result;

            string path = GetPathForKeyCode(key);
            if (path == null) return result;

            var buttonsField = AccessTools.Field(typeof(ZInput), "m_buttons");
            if (buttonsField?.GetValue(ZInput.instance) is Dictionary<string, ZInput.ButtonDef> buttons)
            {
                foreach (var kvp in buttons)
                {
                    if (kvp.Value.GetActionPath() == path)
                        result.Add(kvp.Key);
                }
            }

            return result;
        }

        private static string GetPathForKeyCode(KeyCode key)
        {
            if (_keyCodeToPathMethod == null)
                _keyCodeToPathMethod = AccessTools.Method(typeof(ZInput), "KeyCodeToPath",
                    new[] { typeof(KeyCode), typeof(bool) });
            return _keyCodeToPathMethod?.Invoke(null, new object[] { key, false }) as string;
        }
    }
}
