using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Fixes a cascading NullReferenceException in ZNetScene.RemoveObjects.
    //
    // Root cause: ZNetView.OnDestroy does not remove the ZNetView from
    // ZNetScene.m_instances. When something calls Object.Destroy(go) directly
    // (bypassing ZNetScene.Destroy), the ZNetView's native object is destroyed
    // but its entry lingers in m_instances. RemoveObjects later tries to call
    // zNetView.gameObject on the dead reference → MissingReferenceException →
    // the m_instances.Remove that follows is skipped, leaving a zombie with a
    // null ZDO that causes the same NRE on every subsequent Update tick.
    //
    // Fix: if OnDestroy fires while the ZDO is still set (meaning ZNetScene.Destroy
    // never ran), remove the entry from m_instances and destroy the ZDO if owned.
    [HarmonyPatch(typeof(ZNetView), "OnDestroy")]
    public static class ZNetViewOnDestroyPatch
    {
        private static readonly FieldInfo InstancesField =
            AccessTools.Field(typeof(ZNetScene), "m_instances");

        [HarmonyPrefix]
        public static void Prefix(ZNetView __instance)
        {
            if (ZNetScene.instance == null) return;

            ZDO zdo = __instance.GetZDO();
            if (zdo == null) return; // ZNetScene.Destroy already cleaned up

            var instances = InstancesField?.GetValue(ZNetScene.instance)
                as Dictionary<ZDO, ZNetView>;
            if (instances == null || !instances.ContainsKey(zdo)) return;

            instances.Remove(zdo);

            if (zdo.IsOwner())
                ZDOMan.instance?.DestroyZDO(zdo);
        }
    }
}
