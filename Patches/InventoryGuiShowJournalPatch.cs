using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Inventory ⇄ Journal open symmetry, inventory side: opening the
    // inventory over the open journal swaps panels instead of stacking.
    // Tab reaches InventoryGui.Update's Show(null) while the journal is up
    // because that path reads the REAL animator visibility (not the patched
    // IsVisible()) and isn't TakeInput-gated — so the journal yields here.
    // The journal-side swap (journal hotkey over the open inventory) lives
    // in HotkeyInputPatch.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class InventoryGuiShowJournalPatch
    {
        public static void Prefix()
        {
            if (JournalUI.IsOpen) JournalUI.Close();
        }
    }
}
