using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Blocks player input while the journal panel is open — mirrors the
    // vanilla pattern where every gameplay action in Player.Update gates
    // on TakeInput(), and TakeInput already returns false for vanilla
    // modals (Inventory, Menu, StoreGui, etc.). Forcing __result to
    // false when JournalUI.IsOpen extends that mechanism to cover our
    // panel without touching the underlying check chain.
    [HarmonyPatch(typeof(Player), "TakeInput")]
    public static class PlayerTakeInputJournalPatch
    {
        public static void Postfix(ref bool __result)
        {
            if (JournalUI.IsOpen) __result = false;
        }
    }
}
