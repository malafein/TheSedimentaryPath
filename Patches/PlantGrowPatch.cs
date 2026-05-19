using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // When a vine sapling matures, broadcast the maturation event to all
    // recorded watchers via VineMaturedRpc so each watcher's client credits
    // the Patience in Bloom feat. Runs on the ZDO owner only.
    //
    // Plant.Grow is called once per maturation — it instantiates the grown
    // prefab and destroys the Plant. By that point TSP_watchers (populated
    // by VineGrowthPatch's RPC handler) holds every PlayerID that contributed
    // watch credit.
    //
    // Only Plants whose grown form has a Vine component count; non-vine
    // crops (carrot, barley, etc.) are ignored via VinerySkill.IsVinePlant.
    [HarmonyPatch(typeof(Plant), "Grow")]
    public static class PlantGrowPatch
    {
        public static void Prefix(Plant __instance, out string __state)
        {
            __state = null;
            if (!VinerySkill.IsVinePlant(__instance)) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            // Capture watchers list in prefix because Plant.Grow destroys the ZDO.
            __state = nview.GetZDO()?.GetString(VinerySkill.ZdoWatchersKey, "");
        }

        public static void Postfix(string __state)
        {
            if (string.IsNullOrEmpty(__state)) return;
            VineMaturedRpc.BroadcastMaturation(__state);
        }
    }
}
