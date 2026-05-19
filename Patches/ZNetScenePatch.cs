using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    public static class ZNetScenePatch
    {
        public static void Postfix(ZNetScene __instance)
        {
            Log.Debug("ZNetScene.Awake: postfix fired");

            // Clear transient per-session state before registering handlers, so
            // any leftovers from a previous world don't survive into the new one.
            AchievementSystem.ClearAll();

            RockShrine.RegisterRPCs();
            VineMaturedRpc.Register();
            CreatureDeathRpc.Register();

            FieldInfo field = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            if (field == null)
            {
                Log.Error("ZNetScene.Awake: m_namedPrefabs field not found");
                return;
            }

            var namedPrefabs = (Dictionary<int, GameObject>)field.GetValue(__instance);
            RegisterPrefab(__instance, namedPrefabs, Plugin.HeftyStonePrefab);
            RegisterPrefab(__instance, namedPrefabs, Plugin.SmoothStonePrefab);
        }

        private static void RegisterPrefab(ZNetScene scene, Dictionary<int, GameObject> namedPrefabs, GameObject prefab)
        {
            if (prefab == null)
                return;

            int hash = prefab.name.GetStableHashCode();
            if (!namedPrefabs.ContainsKey(hash))
            {
                namedPrefabs[hash] = prefab;
                if (prefab.GetComponent<ZNetView>() != null)
                    scene.m_prefabs.Add(prefab);
                Log.Debug($"ZNetScene.Awake: registered {prefab.name} (hash={hash})");
            }
        }
    }
}
