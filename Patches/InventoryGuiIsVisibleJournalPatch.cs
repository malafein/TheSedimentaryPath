using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Makes the game treat the open journal panel as a visible inventory-class
    // modal. InventoryGui.IsVisible() is the single lever the vanilla input
    // pipeline reads to produce "menu open" behaviour:
    //   • GameCamera.UpdateMouseCapture frees the cursor (so the player can
    //     click the panel) when !InventoryGui.IsVisible() is false.
    //   • PlayerController.LateUpdate zeroes mouse-look via InInventoryEtc().
    //   • PlayerController.FixedUpdate suppresses attack/block/dodge/crouch
    //     via InInventoryEtc(), while WASD movement still flows to SetControls
    //     — matching vanilla, where the inventory leaves you free to walk.
    //   • Player.TakeInput() returns false (it ANDs in !InventoryGui.IsVisible()),
    //     suppressing interact/use/hotbar in Player.Update.
    // One hook gives exact vanilla-inventory parity, so no separate input or
    // cursor patch is needed.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
    public static class InventoryGuiIsVisibleJournalPatch
    {
        public static void Postfix(ref bool __result)
        {
            if (JournalUI.IsOpen) __result = true;
        }
    }
}
