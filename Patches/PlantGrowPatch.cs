using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // When a vine sapling matures, broadcast the maturation event to all
    // recorded watchers via VineMaturedRpc so each watcher's client credits
    // the Patience in Bloom feat. Runs on the ZDO owner only.
    //
    // Plant.SUpdate calls Grow every ~10s once past grow time, but Grow is a
    // no-op (returns null) while the plant is unhealthy (NoSpace/NoSun/...);
    // only the healthy call instantiates the grown prefab and destroys the
    // Plant. The postfix must gate on __result, or a blocked watched sapling
    // credits the feat every 10 seconds forever. By maturation, TSP_watchers
    // (populated by VineGrowthPatch's RPC handler) holds every PlayerID that
    // contributed watch credit.
    //
    // Only Plants whose grown form has a Vine component count; non-vine
    // crops (carrot, barley, etc.) are ignored via VinerySkill.IsVinePlant.
    [HarmonyPatch(typeof(Plant), "Grow")]
    public static class PlantGrowPatch
    {
        public static void Prefix(Plant __instance, out string __state)
        {
            __state = null;

            // Reset the growth context each call; it's set below only for a vine plant
            // and cleared in Postfix, so it's live exactly for this Grow's Instantiate
            // (the grown vine's Awake, which runs synchronously inside it).
            BindsinewVine.s_growingFromPlant = false;
            BindsinewVine.s_growingFromPlantWatched = false;

            if (!VinerySkill.IsVinePlant(__instance)) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            // Capture watchers list in prefix because Plant.Grow destroys the ZDO.
            __state = nview.GetZDO()?.GetString(VinerySkill.ZdoWatchersKey, "");

            // Hand the grown vine its lineage: planted → tended; watched-as-sapling
            // (had watchers) → watched. Read by BindsinewVine.ConfigureInstance.
            BindsinewVine.s_growingFromPlant = true;
            BindsinewVine.s_growingFromPlantWatched = !string.IsNullOrEmpty(__state);
        }

        public static void Postfix(
            GameObject __result,
            string __state)
        {
            BindsinewVine.s_growingFromPlant = false;
            BindsinewVine.s_growingFromPlantWatched = false;

            // Grow returns null when the plant couldn't grow (unhealthy) —
            // nothing matured, so no feat credit.
            if (__result == null) return;

            if (string.IsNullOrEmpty(__state)) return;
            VineMaturedRpc.BroadcastMaturation(__state);
        }
    }
}
