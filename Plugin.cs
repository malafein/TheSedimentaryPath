using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.malafein.thesedimentarypath";
        public const string ModName = "The Sedimentary Path";
        public const string ModVersion = "0.2.3";

        public static GameObject HeftyStonePrefab;
        public static GameObject SmoothStonePrefab;
        public static GameObject SmoothStoneProjectilePrefab;
        public static GameObject KaldmorkPrefab;
        public static GameObject KaldmorkProjectilePrefab;
        public static GameObject DokkbladPrefab;
        public static GameObject BlackstoneBrewBasePrefab;

        // Keyed by $item_* name. Any weapon implementing IStanceWeapon registers here
        // so the hotkey and equip patches can route without knowing weapon names.
        public static readonly System.Collections.Generic.Dictionary<string, IStanceWeapon> StanceWeapons
            = new System.Collections.Generic.Dictionary<string, IStanceWeapon>();

        // Keyed by $item_* name → secondary skill type. Weapons in this registry
        // derive their skill factor from a 50/50 blend of their native weapon skill
        // and the secondary skill, and split XP evenly between the two on each hit.
        // Add Rockery weapons at registration; Vinery weapons follow the same pattern.
        public static readonly System.Collections.Generic.Dictionary<string, ValheimSkills.SkillType> SplitSkillWeapons
            = new System.Collections.Generic.Dictionary<string, ValheimSkills.SkillType>();
        public static GameObject BlackstoneBrewPrefab;
        public static GameObject VineberryJuiceBasePrefab;
        public static GameObject VineberryJuicePrefab;

#if DEBUG
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<float> ShrineIntervalDebug;
        public static ConfigEntry<float> HeldOffsetX;
        public static ConfigEntry<float> HeldOffsetY;
        public static ConfigEntry<float> HeldOffsetZ;
        public static ConfigEntry<float> HeldRotX;
        public static ConfigEntry<float> HeldRotY;
        public static ConfigEntry<float> HeldRotZ;
        public static ConfigEntry<float> HeldScale;
#endif

        public static bool IsDebugMode =>
#if DEBUG
            DebugMode?.Value ?? false;
#else
            false;
#endif

        public static ConfigEntry<bool> RockeryProximityAlert;
        public static ConfigEntry<bool> RockeryProximityEffect;
        public static ConfigEntry<bool> VineryProximityAlert;
        public static ConfigEntry<bool> VineryProximityEffect;

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
        public static ConfigEntry<KeyboardShortcut> ToggleWeaponStance;
        public static ConfigEntry<KeyboardShortcut> JournalHotkey;
        public static ConfigEntry<float> JournalScrollSensitivity;

        public static ConfigEntry<bool> DetectVines;
        public static ConfigEntry<bool> DetectBerries;
        public static ConfigEntry<bool> DetectMushrooms;
        public static ConfigEntry<bool> DetectFieldCrops;
        public static ConfigEntry<bool> DetectHerbs;

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            Log.Info($"{ModName} {ModVersion} is loading...");

            ConfigEntry<bool> configLocked = Config.Bind("Server", "Lock Configuration", true,
                "Configuration is locked and can only be changed by server admins.");
            configSync.AddLockingConfigEntry(configLocked);

#if DEBUG
            DebugMode = ClientConfig("Debug", "DebugMode", false,
                "When enabled, shows ZDO IDs and raw credit/skill-factor values in vine and plant hover text.");
            ShrineIntervalDebug = ClientConfig("Debug", "ShrineIntervalSeconds", 0f,
                "Debug: override rock shrine conversion check interval in seconds. 0 = default (1800s / one Valheim day).");

            const string heldSection = "Debug - HeftyStone Held";
            HeldOffsetX = ClientConfig(heldSection, "OffsetX", HeftyStone.HeldOffsetX, "X position offset for mesh within attach node");
            HeldOffsetY = ClientConfig(heldSection, "OffsetY", HeftyStone.HeldOffsetY, "Y position offset (up into palm)");
            HeldOffsetZ = ClientConfig(heldSection, "OffsetZ", HeftyStone.HeldOffsetZ, "Z position offset (forward/back in grip)");
            HeldRotX = ClientConfig(heldSection, "RotationX", HeftyStone.HeldRotX, "X euler rotation for mesh");
            HeldRotY = ClientConfig(heldSection, "RotationY", HeftyStone.HeldRotY, "Y euler rotation for mesh");
            HeldRotZ = ClientConfig(heldSection, "RotationZ", HeftyStone.HeldRotZ, "Z euler rotation for mesh");
            HeldScale = ClientConfig(heldSection, "Scale", HeftyStone.HeldScale, "Scale multiplier for mesh");

            Config.SettingChanged += OnSettingChanged;
#endif

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

            ToggleWeaponStance = ClientConfig("Combat", "WeaponStanceHotkey", new KeyboardShortcut(KeyCode.G),
                "Hotkey to toggle the stance of weapons that support it (e.g. throw / leap). Only fires when such a weapon is equipped.");

            JournalHotkey = ClientConfig("Journal", "ToggleHotkey", new KeyboardShortcut(KeyCode.J),
                "Hotkey to open and close the in-game journal.");
            JournalScrollSensitivity = ClientConfig("Journal", "ScrollSensitivity", 300f,
                "Mouse-wheel scroll speed in the journal's lists. Higher scrolls faster. Re-applied each time the journal is opened.");

            DetectVines = ClientConfig("Vinery Categories", "DetectVines", true, "Sense Vines and Vineberries.");
            DetectBerries = ClientConfig("Vinery Categories", "DetectBerries", true, "Sense Raspberries, Blueberries, and Cloudberries.");
            DetectMushrooms = ClientConfig("Vinery Categories", "DetectMushrooms", true, "Sense all types of Mushrooms.");
            DetectFieldCrops = ClientConfig("Vinery Categories", "DetectFieldCrops", true, "Sense agricultural crops like Turnips, Carrots, Onions, Barley, and Flax.");
            DetectHerbs = ClientConfig("Vinery Categories", "DetectHerbs", true, "Sense wild herbs like Thistle and Dandelion.");

            harmony.PatchAll();

            // Touch FeatRegistry to force its static constructor to run at startup
            // (otherwise it lazy-loads on first patch hit, hiding load issues).
            _ = malafein.Valheim.TheSedimentaryPath.Journal.FeatRegistry.Get("rocks_collected");

            // Register boons + their rituals.
            malafein.Valheim.TheSedimentaryPath.Journal.TSPBoons.RegisterAll();

            // Register lore entries (depends on TSPBoons being registered
            // first because BoonTierReached conditions reference boon IDs).
            malafein.Valheim.TheSedimentaryPath.Journal.TSPLore.RegisterAll();

#if DEBUG
            DebugCommands.Register();
#endif

            Log.Info($"{ModName} loaded");
        }

#if DEBUG
        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (HeftyStonePrefab == null)
                return;

            string section = args.ChangedSetting.Definition.Section;
            if (!section.StartsWith("Debug - HeftyStone"))
                return;

            Log.Debug($"Config changed: {section}/{args.ChangedSetting.Definition.Key} — re-applying mesh transforms");
            HeftyStone.ApplyMeshTransforms(HeftyStonePrefab);
            SmoothStone.ApplyMeshTransforms(SmoothStonePrefab);
        }
#endif
    }
}
