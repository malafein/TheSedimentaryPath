using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.malafein.thesedimentarypath";
        public const string ModName = "The Sedimentary Path";
        public const string ModVersion = "0.0.13";

        public static GameObject HeftyStonePrefab;
        public static GameObject SmoothStonePrefab;
        public static GameObject BlackstoneBrewBasePrefab;
        public static GameObject BlackstoneBrewPrefab;
        public static GameObject VineberryJuiceBasePrefab;
        public static GameObject VineberryJuicePrefab;

        // Debug mode: shows ZDO IDs and raw credit values in vine hover text
        public static ConfigEntry<bool> DebugMode;

        public static ConfigEntry<bool> RockeryProximityAlert;
        public static ConfigEntry<bool> RockeryProximityEffect;
        public static ConfigEntry<bool> VineryProximityAlert;
        public static ConfigEntry<bool> VineryProximityEffect;

        // Debug config entries for mesh positioning (held in hand)
        public static ConfigEntry<float> HeldOffsetX;
        public static ConfigEntry<float> HeldOffsetY;
        public static ConfigEntry<float> HeldOffsetZ;
        public static ConfigEntry<float> HeldRotX;
        public static ConfigEntry<float> HeldRotY;
        public static ConfigEntry<float> HeldRotZ;
        public static ConfigEntry<float> HeldScale;

        // Inactive container for cloned prefabs. Clones parented here have
        // activeSelf=true but activeInHierarchy=false, which prevents
        // ZNetView.Awake() from firing while keeping meshes readable by VisEquipment.
        private static GameObject _prefabContainer;
        public static Transform PrefabContainer
        {
            get
            {
                if (_prefabContainer == null)
                {
                    _prefabContainer = new GameObject("TheSedimentaryPath_Prefabs");
                    Object.DontDestroyOnLoad(_prefabContainer);
                    _prefabContainer.SetActive(false);
                }
                return _prefabContainer.transform;
            }
        }

        internal static readonly ConfigSync configSync = new ConfigSync(ModGUID)
        {
            DisplayName = ModName,
            CurrentVersion = ModVersion,
            MinimumRequiredVersion = ModVersion,
            ModRequired = true
        };

        private readonly Harmony harmony = new Harmony(ModGUID);

        private static ConfigEntry<T> ClientConfig<T>(string section, string key, T defaultValue, string description)
        {
            var entry = Plugin.Instance.Config.Bind(section, key, defaultValue, description);
            configSync.AddConfigEntry(entry).SynchronizedConfig = false;
            return entry;
        }

        public static Plugin Instance { get; private set; }

        public static ConfigEntry<KeyboardShortcut> ToggleRockeryProximity;
        public static ConfigEntry<KeyboardShortcut> ToggleVineryProximity;
        public static ConfigEntry<KeyboardShortcut> DebugSkillSet25;
        public static ConfigEntry<KeyboardShortcut> DebugSkillSet50;

        public static ConfigEntry<bool> DetectVines;
        public static ConfigEntry<bool> DetectBerries;
        public static ConfigEntry<bool> DetectMushrooms;
        public static ConfigEntry<bool> DetectFieldCrops;
        public static ConfigEntry<bool> DetectHerbs;

        private void Awake()
        {
            Instance = this;
            ZLog.Log($"{ModName} {ModVersion} is loading...");

            ConfigEntry<bool> configLocked = Config.Bind("Server", "Lock Configuration", true,
                "Configuration is locked and can only be changed by server admins.");
            configSync.AddLockingConfigEntry(configLocked);

            DebugMode = ClientConfig("Debug", "DebugMode", false,
                "When enabled, shows ZDO IDs and raw credit/skill-factor values in vine and plant hover text.");
            DebugSkillSet25 = ClientConfig("Debug", "SkillSet25Hotkey", new KeyboardShortcut(KeyCode.F7),
                "Debug: set Rockery and Vinery skills to 25. Only fires when DebugMode is on.");
            DebugSkillSet50 = ClientConfig("Debug", "SkillSet50Hotkey", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftAlt),
                "Debug: set Rockery and Vinery skills to 50. Only fires when DebugMode is on.");

            RockeryProximityAlert = ClientConfig("Rockery", "ProximityAlert", true,
                "When enabled, sufficiently skilled practitioners may sense nearby harvestable stone.");
            RockeryProximityEffect = ClientConfig("Rockery", "ProximityEffect", true,
                "When enabled, a deeper mastery of Rockery may unlock an additional sense.");
            ToggleRockeryProximity = ClientConfig("Rockery", "ToggleHotkey", new KeyboardShortcut(KeyCode.R, KeyCode.LeftAlt),
                "Hotkey to quickly toggle the Rockery proximity sense on and off.");

            VineryProximityAlert = ClientConfig("Vinery", "ProximityAlert", true,
                "When enabled, sufficiently skilled practitioners may sense nearby harvestable growth.");
            VineryProximityEffect = ClientConfig("Vinery", "ProximityEffect", true,
                "When enabled, a deeper mastery of Vinery may unlock an additional sense.");
            ToggleVineryProximity = ClientConfig("Vinery", "ToggleHotkey", new KeyboardShortcut(KeyCode.V, KeyCode.LeftAlt),
                "Hotkey to quickly toggle the Vinery proximity sense on and off.");

            DetectVines = ClientConfig("Vinery Categories", "DetectVines", true, "Sense Vines and Vineberries.");
            DetectBerries = ClientConfig("Vinery Categories", "DetectBerries", true, "Sense Raspberries, Blueberries, and Cloudberries.");
            DetectMushrooms = ClientConfig("Vinery Categories", "DetectMushrooms", true, "Sense all types of Mushrooms.");
            DetectFieldCrops = ClientConfig("Vinery Categories", "DetectFieldCrops", true, "Sense agricultural crops like Turnips, Carrots, Onions, Barley, and Flax.");
            DetectHerbs = ClientConfig("Vinery Categories", "DetectHerbs", true, "Sense wild herbs like Thistle and Dandelion.");

            const string section = "Debug - HeftyStone Held";
            HeldOffsetX = ClientConfig(section, "OffsetX", 0.08f, "X position offset for mesh within attach node");
            HeldOffsetY = ClientConfig(section, "OffsetY", -0.03f, "Y position offset (up into palm)");
            HeldOffsetZ = ClientConfig(section, "OffsetZ", -0.01f, "Z position offset (forward/back in grip)");
            HeldRotX = ClientConfig(section, "RotationX", 150f, "X euler rotation for mesh");
            HeldRotY = ClientConfig(section, "RotationY", -45f, "Y euler rotation for mesh");
            HeldRotZ = ClientConfig(section, "RotationZ", 0f, "Z euler rotation for mesh");
            HeldScale = ClientConfig(section, "Scale", 0.5f, "Scale multiplier for mesh");

            Config.SettingChanged += OnSettingChanged;

            harmony.PatchAll();
            ZLog.Log($"{ModName} loaded!");
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (HeftyStonePrefab == null)
                return;

            string section = args.ChangedSetting.Definition.Section;
            if (!section.StartsWith("Debug - HeftyStone"))
                return;

            ZLog.Log($"[TheSedimentaryPath] Config changed: {section}/{args.ChangedSetting.Definition.Key} — re-applying mesh transforms");
            HeftyStone.ApplyMeshTransforms(HeftyStonePrefab);
            SmoothStone.ApplyMeshTransforms(SmoothStonePrefab);
        }
    }
}
