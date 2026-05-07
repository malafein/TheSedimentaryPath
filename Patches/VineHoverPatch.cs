using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    /// <summary>
    /// Appends Vinery credit status to hover text on vine berry pickables and vine plant saplings.
    /// Debug mode additionally shows the ZDO UID and raw credit value.
    /// </summary>
    [HarmonyPatch]
    public static class VineHoverPatch
    {
        private static void AppendVineryStatus(ref string result, ZDO zdo)
        {
            if (!Plugin.IsDebugMode) return;

            float totalWatch = zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f);
            result += $"<size=12>\n[DBG] ZDO: <color=#0FF>{zdo.m_uid}</color> | total watch: {totalWatch:F1}s</size>";
        }

        // Vine berry pickables share the Vine's ZNetView/ZDO
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix_PickableHover(Pickable __instance, ref string __result)
        {
            Vine vine = __instance.GetComponentInParent<Vine>();
            if (vine == null) return;

            ZNetView nview = vine.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            AppendVineryStatus(ref __result, zdo);
        }

        // All cultivated plant saplings
        [HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix_PlantHover(Plant __instance, ref string __result)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            AppendVineryStatus(ref __result, zdo);
        }

        // Wild watchable pickables (berries, mushrooms) — not vine berries (covered above)
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix_WildPickableHover(Pickable __instance, ref string __result)
        {
            if (__instance.GetComponentInParent<Vine>() != null) return;
            if (!VinerySkill.IsVineryWatchable(__instance)) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            AppendVineryStatus(ref __result, zdo);
        }

        // Debug-only: show proximity detector eligibility on every Pickable
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix_DebugPickableInfo(Pickable __instance, ref string __result)
        {
            if (!Plugin.IsDebugMode) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            bool hasVine      = __instance.GetComponentInParent<Vine>() != null;
            bool isWatchable  = VinerySkill.IsVineryWatchable(__instance);
            bool isPicked     = __instance.GetPicked();
            string prefab     = Utils.GetPrefabName(__instance.gameObject);
            string layer      = LayerMask.LayerToName(__instance.gameObject.layer);

            // Would VineryProximityDetector count this object?
            bool proximityHit = (hasVine || isWatchable) && !isPicked;

            __result += $"<size=11>\n<color=#0FF>[DBG] ZDO={zdo.m_uid} prefab={prefab}</color>" +
                        $"\n<color=#0FF>layer={layer} hasVine={hasVine} watchable={isWatchable} picked={isPicked} → prox={proximityHit}</color></size>";
        }
    }
}
