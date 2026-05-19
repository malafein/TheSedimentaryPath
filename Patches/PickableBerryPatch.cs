using System.Collections.Generic;
using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Tracks berry pickups while drunk for The Swaying Harvest feat.
    // Fires on any berry-class Pickable, including Vineberry. (Vineberry also
    // counts toward vineberries_harvested via PickableVineryPatch — different
    // feats, different concerns.)
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    public static class PickableBerryPatch
    {
        private static readonly HashSet<string> BerryPickables = new HashSet<string>
        {
            "Pickable_Blueberries",
            "Pickable_Raspberry",
            "Pickable_Cloudberry",
            "Pickable_Vineberry",
        };

        public static void Postfix(Pickable __instance, Humanoid character, bool __result)
        {
            if (!__result) return;
            if (!(character is Player player)) return;
            if (!FeatTracker.IsDrunk(player)) return;

            string pickableName = Utils.GetPrefabName(__instance.gameObject);
            if (!BerryPickables.Contains(pickableName)) return;

            FeatTracker.RecordEvent(player, Feats.BerriesForagedDrunk);
        }
    }
}
