using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Items;
using malafein.Valheim.TheSedimentaryPath.Skills;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDBPatch
    {
        private static readonly FieldInfo ItemByHashField =
            AccessTools.Field(typeof(ObjectDB), "m_itemByHash");

        public static void Postfix(ObjectDB __instance)
        {
            Log.Debug($"ObjectDB.Awake: m_items.Count={__instance.m_items.Count}");

            // Reset the Wishbone effect cache so it re-fetches from this ObjectDB instance.
            WishboneEffects.Reset();

            if (__instance.m_items.Count == 0)
            {
                Log.Debug("ObjectDB.Awake: skipping (title screen, no items)");
                return;
            }

            // Register localization first (needed for both items)
            AddLocalization();

            // Register Hefty Stone
            if (__instance.GetItemPrefab("HeftyStone") == null)
            {
                GameObject heftyPrefab = HeftyStone.CreatePrefab();
                if (heftyPrefab != null)
                {
                    Plugin.HeftyStonePrefab = heftyPrefab;
                    RegisterItem(__instance, heftyPrefab);
                    RegisterInZNetScene(heftyPrefab);
                    AddHeftyStoneRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Hefty Stone registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: HeftyStone.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: HeftyStone already registered, skipping");
            }

            // Register Blackstone Brew (base + fermented) + SE
            if (__instance.GetItemPrefab("BlackstoneBrew") == null)
            {
                StatusEffect brewSE = BlackstoneBrew.CreateStatusEffect();
                if (brewSE != null)
                {
                    __instance.m_StatusEffects.Add(brewSE);
                    Log.Debug("ObjectDB.Awake: SE_BlackstoneBrew registered");
                }

                GameObject basePrefab = BlackstoneBrew.CreateBasePrefab();
                if (basePrefab != null)
                {
                    Plugin.BlackstoneBrewBasePrefab = basePrefab;
                    RegisterItem(__instance, basePrefab);
                    RegisterInZNetScene(basePrefab);
                    Log.Debug("ObjectDB.Awake: BlackstoneBrewBase registered");
                }

                GameObject brewPrefab = BlackstoneBrew.CreateBrewPrefab(brewSE);
                if (brewPrefab != null)
                {
                    Plugin.BlackstoneBrewPrefab = brewPrefab;
                    RegisterItem(__instance, brewPrefab);
                    RegisterInZNetScene(brewPrefab);
                    AddBlackstoneBrewBaseRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: BlackstoneBrew registered");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: BlackstoneBrew already registered, skipping");
            }

            // Register Vineberry Juice (base + fermented) + SE
            if (__instance.GetItemPrefab("VineberryJuice") == null)
            {
                StatusEffect juiceSE = VineberryJuice.CreateStatusEffect();
                if (juiceSE != null)
                {
                    __instance.m_StatusEffects.Add(juiceSE);
                    Log.Debug("ObjectDB.Awake: SE_VineberryJuice registered");
                }

                GameObject juiceBasePrefab = VineberryJuice.CreateBasePrefab();
                if (juiceBasePrefab != null)
                {
                    Plugin.VineberryJuiceBasePrefab = juiceBasePrefab;
                    RegisterItem(__instance, juiceBasePrefab);
                    RegisterInZNetScene(juiceBasePrefab);
                    Log.Debug("ObjectDB.Awake: VineberryJuiceBase registered");
                }

                GameObject juicePrefab = VineberryJuice.CreateJuicePrefab(juiceSE);
                if (juicePrefab != null)
                {
                    Plugin.VineberryJuicePrefab = juicePrefab;
                    RegisterItem(__instance, juicePrefab);
                    RegisterInZNetScene(juicePrefab);
                    AddVineberryJuiceBaseRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: VineberryJuice registered");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: VineberryJuice already registered, skipping");
            }

            // Register the stance status effect once (shared by all stance weapons)
            if (!__instance.m_StatusEffects.Exists(se => se is SE_WeaponStance))
            {
                SE_WeaponStance stanceSE = ScriptableObject.CreateInstance<SE_WeaponStance>();
                stanceSE.name  = "SE_WeaponStance";
                stanceSE.m_name = "$se_weaponstance";
                stanceSE.m_ttl  = 3600f;
                __instance.m_StatusEffects.Add(stanceSE);
                Log.Debug("ObjectDB.Awake: SE_WeaponStance registered");
            }

            // Register the Stone-Kin status effect template. Tier-specific
            // configuration (m_ttl, damage mods, KinFist damage) is set
            // per-instance by SE_StoneKin.Initialize() after SEMan adds
            // the clone.
            if (!__instance.m_StatusEffects.Exists(se => se is SE_StoneKin))
            {
                SE_StoneKin stoneKinSE = ScriptableObject.CreateInstance<SE_StoneKin>();
                stoneKinSE.name = "SE_StoneKin";
                stoneKinSE.m_name = "Stone-Kin";
                stoneKinSE.m_tooltip = "The Rock knows you as kin.";
                // HUD status-bar icon. Without an m_icon the effect is active
                // but invisible in the status list. Borrow the Stone Golem
                // trophy icon (placeholder until a custom kin icon lands —
                // see dev/journal-ui-styling-pass.md).
                stoneKinSE.m_icon = GetItemIcon(__instance, "TrophySGolem");
                if (stoneKinSE.m_icon == null)
                    Log.Warn("ObjectDB.Awake: TrophySGolem icon not found; SE_StoneKin will have no HUD icon");
                __instance.m_StatusEffects.Add(stoneKinSE);
                Log.Debug("ObjectDB.Awake: SE_StoneKin registered");
            }

            // Register the Holdfast status effect template — the Vinery
            // boon. Tier-specific configuration (m_ttl, tier mods, root-hold
            // duration) is set per-instance by SE_Holdfast.Initialize()
            // after SEMan adds the clone.
            if (!__instance.m_StatusEffects.Exists(se => se is SE_Holdfast))
            {
                SE_Holdfast holdfastSE = ScriptableObject.CreateInstance<SE_Holdfast>();
                holdfastSE.name = SE_Holdfast.EffectName;
                holdfastSE.m_name = "Holdfast";
                holdfastSE.m_tooltip = "The vine holds fast to you.";
                // Borrow the Abomination trophy icon (placeholder until a
                // custom icon lands, same as Stone-Kin's golem trophy).
                holdfastSE.m_icon = GetItemIcon(__instance, "TrophyAbomination");
                if (holdfastSE.m_icon == null)
                    Log.Warn("ObjectDB.Awake: TrophyAbomination icon not found; SE_Holdfast will have no HUD icon");
                __instance.m_StatusEffects.Add(holdfastSE);
                Log.Debug("ObjectDB.Awake: SE_Holdfast registered");
            }

            // Build KinFist (hidden ItemData, not in inventory).
            // Tier-3 KinFist activation reads its damage from this
            // shared instance via Humanoid.GetCurrentWeapon postfix.
            KinFist.Build();

            // Marker SE for rockery kill attribution — must exist before the obsidian
            // weapons register (their SharedData points at it; see StoneStatusEffects).
            StoneStatusEffects.Build();
            AddStatusEffectIfMissing(__instance, StoneStatusEffects.Mark);

            // Register Kaldmörk (obsidian frost dagger)
            if (__instance.GetItemPrefab("Kaldmork") == null)
            {
                var kaldmorkWeapon = new ObsidianDagger();
                GameObject kaldmorkPrefab = kaldmorkWeapon.CreatePrefab();
                if (kaldmorkPrefab != null)
                {
                    Plugin.KaldmorkPrefab = kaldmorkPrefab;
                    RegisterItem(__instance, kaldmorkPrefab);
                    RegisterInZNetScene(kaldmorkPrefab);

                    if (kaldmorkWeapon.ProjectilePrefab != null)
                    {
                        Plugin.KaldmorkProjectilePrefab = kaldmorkWeapon.ProjectilePrefab;
                        RegisterInZNetScene(kaldmorkWeapon.ProjectilePrefab);
                    }

                    Plugin.StanceWeapons["$item_kaldmork"]    = kaldmorkWeapon;
                    Plugin.SplitSkillWeapons["$item_kaldmork"] = RockerySkill.SkillType;
                    AddKaldmorkRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Kaldmörk registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: ObsidianDagger.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: Kaldmörk already registered, skipping");
            }

            // Register Dökkblað (obsidian frost sword) — must follow dagger registration (clones KaldmorkPrefab)
            if (__instance.GetItemPrefab("Dokkblad") == null)
            {
                var dokkbladWeapon = new ObsidianSword();
                GameObject dokkbladPrefab = dokkbladWeapon.CreatePrefab();
                if (dokkbladPrefab != null)
                {
                    Plugin.DokkbladPrefab = dokkbladPrefab;
                    RegisterItem(__instance, dokkbladPrefab);
                    RegisterInZNetScene(dokkbladPrefab);

                    Plugin.StanceWeapons["$item_dokkblad"]    = dokkbladWeapon;
                    Plugin.SplitSkillWeapons["$item_dokkblad"] = RockerySkill.SkillType;
                    AddDokkbladRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Dökkblað registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: ObsidianSword.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: Dökkblað already registered, skipping");
            }

            // Bindsinew — the cultivated vine fiber both vinery weapons are built from.
            // Register before the weapons so their recipes can reference it.
            if (__instance.GetItemPrefab(TSPVineryWeapons.BindsinewPrefab) == null)
            {
                GameObject bindsinew = Bindsinew.CreatePrefab();
                if (bindsinew != null)
                {
                    Plugin.BindsinewPrefab = bindsinew;
                    RegisterItem(__instance, bindsinew);
                    RegisterInZNetScene(bindsinew);
                    // Repoint the green vine's pickable at Bindsinew (tended+watched vines
                    // only — see BindsinewVine). Needs the Bindsinew prefab registered first.
                    BindsinewVine.FindAndRepurpose();
                    Log.Debug("ObjectDB.Awake: Bindsinew registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: Bindsinew.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: Bindsinew already registered, skipping");
            }

            // Vinery empowers the vanilla Ashlands Nature (jade) weapons — capture
            // their authored proc chances before any per-swing scaling mutates them.
            NatureWeaponEmpowerment.Build(__instance);

            // Vinery weapons (RootAtgeir + RootSpear) — split skill (native weapon
            // skill × Vinery), mirroring the rockery obsidian weapons. Both share the
            // vine on-hit effects, so build + register those first.
            VineStatusEffects.Build(__instance);
            AddStatusEffectIfMissing(__instance, VineStatusEffects.Snare);
            AddStatusEffectIfMissing(__instance, VineStatusEffects.Root);
            AddStatusEffectIfMissing(__instance, VineStatusEffects.Tether);

            // Register Root Atgeir (Polearms × Vinery) — the AoE "field" weapon.
            if (__instance.GetItemPrefab(TSPVineryWeapons.RootAtgeirPrefab) == null)
            {
                var rootAtgeir = new RootAtgeir();
                GameObject prefab = rootAtgeir.CreatePrefab();
                if (prefab != null)
                {
                    Plugin.RootAtgeirPrefab = prefab;
                    RegisterItem(__instance, prefab);
                    RegisterInZNetScene(prefab);

                    if (rootAtgeir.HarrowHitVfxPrefab != null)
                        RegisterInZNetScene(rootAtgeir.HarrowHitVfxPrefab);

                    Plugin.StanceWeapons[TSPVineryWeapons.RootAtgeirName]    = rootAtgeir;
                    Plugin.SplitSkillWeapons[TSPVineryWeapons.RootAtgeirName] = VinerySkill.SkillType;
                    AddRootAtgeirRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Root Atgeir registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: RootAtgeir.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: Root Atgeir already registered, skipping");
            }

            // Register Root Spear (Spears × Vinery) — the single-target "line" weapon.
            if (__instance.GetItemPrefab(TSPVineryWeapons.RootSpearPrefab) == null)
            {
                var rootSpear = new RootSpear();
                GameObject prefab = rootSpear.CreatePrefab();
                if (prefab != null)
                {
                    Plugin.RootSpearPrefab = prefab;
                    RegisterItem(__instance, prefab);
                    RegisterInZNetScene(prefab);

                    if (rootSpear.ProjectilePrefab != null)
                    {
                        Plugin.RootSpearProjectilePrefab = rootSpear.ProjectilePrefab;
                        RegisterInZNetScene(rootSpear.ProjectilePrefab);
                    }

                    Plugin.StanceWeapons[TSPVineryWeapons.RootSpearName]    = rootSpear;
                    Plugin.SplitSkillWeapons[TSPVineryWeapons.RootSpearName] = VinerySkill.SkillType;
                    AddRootSpearRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Root Spear registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: RootSpear.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: Root Spear already registered, skipping");
            }

            // Register Smooth Stone
            if (__instance.GetItemPrefab("SmoothStone") == null)
            {
                GameObject smoothPrefab = SmoothStone.CreatePrefab();
                if (smoothPrefab != null)
                {
                    Plugin.SmoothStonePrefab = smoothPrefab;
                    RegisterItem(__instance, smoothPrefab);
                    RegisterInZNetScene(smoothPrefab);

                    if (SmoothStone.ProjectilePrefab != null)
                    {
                        Plugin.SmoothStoneProjectilePrefab = SmoothStone.ProjectilePrefab;
                        RegisterInZNetScene(SmoothStone.ProjectilePrefab);
                    }

                    AddSmoothStoneRecipe(__instance);
                    Log.Debug("ObjectDB.Awake: Smooth Stone registered");
                }
                else
                {
                    Log.Error("ObjectDB.Awake: SmoothStone.CreatePrefab returned null");
                }
            }
            else
            {
                Log.Debug("ObjectDB.Awake: SmoothStone already registered, skipping");
            }
        }

        // First inventory icon of a vanilla item prefab, or null if the
        // prefab / its ItemDrop / icons are missing.
        private static Sprite GetItemIcon(ObjectDB db, string prefabName)
        {
            GameObject prefab = db.GetItemPrefab(prefabName);
            ItemDrop itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            Sprite[] icons = itemDrop?.m_itemData?.m_shared?.m_icons;
            return (icons != null && icons.Length > 0) ? icons[0] : null;
        }

        // Adds a status effect to ObjectDB only if that exact instance isn't already
        // present (the vinery effects are shared static instances reused across
        // ObjectDB.Awake calls).
        private static void AddStatusEffectIfMissing(ObjectDB db, StatusEffect se)
        {
            if (se == null || db.m_StatusEffects.Contains(se)) return;
            db.m_StatusEffects.Add(se);
            Log.Debug($"AddStatusEffectIfMissing: {se.name} registered");
        }

        private static void RegisterItem(ObjectDB db, GameObject prefab)
        {
            db.m_items.Add(prefab);
            if (ItemByHashField != null)
            {
                var itemByHash = (Dictionary<int, GameObject>)ItemByHashField.GetValue(db);
                int hash = prefab.name.GetStableHashCode();
                itemByHash[hash] = prefab;
                Log.Debug($"RegisterItem: {prefab.name} (hash={hash})");
            }
            else
            {
                Log.Error($"RegisterItem: m_itemByHash field not found for {prefab.name}");
            }
        }

        private static void RegisterInZNetScene(GameObject prefab)
        {
            if (ZNetScene.instance == null)
            {
                Log.Warn("RegisterInZNetScene: ZNetScene.instance is null");
                return;
            }

            FieldInfo field = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            if (field == null)
            {
                Log.Error("RegisterInZNetScene: m_namedPrefabs field not found");
                return;
            }

            int hash = prefab.name.GetStableHashCode();
            var namedPrefabs = (Dictionary<int, GameObject>)field.GetValue(ZNetScene.instance);

            if (!namedPrefabs.ContainsKey(hash))
            {
                namedPrefabs[hash] = prefab;
                // Also add to m_prefabs if it has a ZNetView (required for spawned instances)
                if (prefab.GetComponent<ZNetView>() != null)
                {
                    ZNetScene.instance.m_prefabs.Add(prefab);
                    Log.Debug($"RegisterInZNetScene: {prefab.name} with ZNetView (hash={hash})");
                }
                else
                {
                    Log.Debug($"RegisterInZNetScene: {prefab.name} without ZNetView (hash={hash})");
                }
            }
            else
            {
                Log.Debug($"RegisterInZNetScene: {prefab.name} already registered");
            }
        }

        private static void AddLocalization()
        {
            Localization loc = Localization.instance;
            if (loc == null)
            {
                Log.Warn("AddLocalization: Localization.instance is null");
                return;
            }

            FieldInfo field = AccessTools.Field(typeof(Localization), "m_translations");
            if (field == null)
            {
                Log.Error("AddLocalization: m_translations field not found");
                return;
            }

            var translations = (Dictionary<string, string>)field.GetValue(loc);
            translations["item_heftystone"] = "Hefty Stone";
            translations["item_heftystone_desc"] = "A carefully chosen stone, dense and well-balanced. Perfect for throwing — or for making a point.";
            translations["item_smoothstone"] = "Smooth Stone";
            translations["item_smoothstone_desc"] = "A flat, water-worn stone that fits perfectly between the fingers. Aerodynamic and precise.";
            translations["item_vineberryjuicebase"]       = "Vineberry Juice Base";
            translations["item_vineberryjuicebase_desc"]  = "A blend of vineberry, fiddlehead sprouts, and cloudberries. Sweet, strange, and not yet finished. The fermenter will decide the rest.";
            translations["item_vineberryjuice"]           = "Vineberry Juice";
            translations["item_vineberryjuice_desc"]      = "Sweet, still, and faintly luminous. The taste of long afternoons. Cloudberry bright. Vineberry deep. Fiddlehead strange — something in the blend knows you.";
            translations["se_vineberryjuice"]             = "Vineberry Juice";
            translations["se_vineberryjuice_tooltip"]     = "+40 stamina, +30 eitr. Something in the blend knows you.";
            translations["se_vineberryjuice_start"]       = "The vine is in your blood.";
            translations["se_vineberryjuice_stop"]        = "The sweetness fades. The vine forgets nothing.";
            translations["item_blackstonebrewbase"]       = "Blackstone Brew Base";
            translations["item_blackstonebrewbase_desc"] = "A thick, obsidian-dark slurry of ground stone, pukeberries, and fermented barley. It smells of ritual and regret. Needs time in the fermenter.";
            translations["item_blackstonebrew"]          = "Blackstone Brew";
            translations["item_blackstonebrew_desc"]     = "The stone does not float. For a time, neither will you.";
            translations["se_blackstonebrew"]            = "Blackstone Brew";
            translations["se_blackstonebrew_tooltip"]    = "The stone is in you now. +40 health, +10 stamina. Your feet feel heavier.";
            translations["se_blackstonebrew_start"]      = "The brew takes hold...";
            translations["se_blackstonebrew_stop"]       = "The stone's blessing fades.";
            translations["item_kaldmork"]        = "Kaldmörk";
            translations["item_kaldmork_desc"]   = "Forged from the dark glass of the mountain. It holds cold the way stone holds memory. A chill that bites the soul before it claims the flesh.";
            translations["kaldmork_stance_throw"] = "Throw stance";
            translations["kaldmork_stance_leap"]  = "Leap stance";
            translations["item_dokkblad"]        = "Dökkblað";
            translations["item_dokkblad_desc"]   = "The dark glass of the mountain, drawn long. It does not reflect. It only cuts.";
            translations["dokkblad_stance_a"]    = "Thrust stance";
            translations["dokkblad_stance_b"]    = "Leap stance";
            translations["item_rootatgeir"]         = "The Furrowing Share";
            translations["item_rootatgeir_desc"]    = "It turns the soil and what stands upon it. The earth takes hold, and holds.";
            translations["rootatgeir_stance_reap"]   = "Reap stance";
            translations["rootatgeir_stance_harrow"] = "Harrow stance";
            translations["rootatgeir_stance_tend"]   = "Tend stance";
            translations["item_rootspear"]          = "Root-Strand Coil";
            translations["item_rootspear_desc"]     = "The earth does not forget; it merely waits to pull you back.";
            translations["rootspear_stance_cast"]    = "Cast stance";
            translations["rootspear_stance_vault"]   = "Vault stance";
            translations["item_bindsinew"]          = "Bindsinew";
            translations["item_bindsinew_desc"]     = "Sinew of the watched vine, still coiling in the hand. It does not ask whose.";
            translations["se_vinesnare"]             = "Snared";
            translations["se_vineroot"]              = "Rooted";
            translations["se_weaponstance"]       = "Weapon Stance";
            translations[$"skill_{RockerySkill.SkillId}"]             = "Rockery";
            translations[$"skill_{RockerySkill.SkillId}_description"]  = "Stone speaks to those who listen.";
            translations["skill_rockery_desc"]                          = "Stone speaks to those who listen.";
            translations[$"skill_{VinerySkill.SkillId}"]               = "Vinery";
            translations[$"skill_{VinerySkill.SkillId}_description"]   = "The patient know: a watched vine does not wither.";
            translations["skill_vinery_desc"]                          = "The patient know: a watched vine does not wither.";
            Log.Debug($"AddLocalization: {translations.Count} total translations");
        }

        private static void AddVineberryJuiceBaseRecipe(ObjectDB db)
        {
            // Mead Kettle recipe: 3 Vineberry + 2 FiddleheadFern + 4 Cloudberries → 1 VineberryJuiceBase
            GameObject vineberry   = db.GetItemPrefab("Vineberry");
            GameObject fiddlehead  = db.GetItemPrefab("Fiddleheadfern");
            GameObject cloudberry  = db.GetItemPrefab("Cloudberry");
            GameObject cauldron    = ZNetScene.instance?.GetPrefab("piece_MeadCauldron");

            if (vineberry == null || fiddlehead == null || cloudberry == null)
            {
                Log.Error("AddVineberryJuiceBaseRecipe: one or more ingredient prefabs not found " +
                    $"(Vineberry={vineberry != null}, Fiddleheadfern={fiddlehead != null}, Cloudberry={cloudberry != null})");
                return;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_VineberryJuiceBase";
            recipe.m_item = Plugin.VineberryJuiceBasePrefab.GetComponent<ItemDrop>();
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = cauldron?.GetComponent<CraftingStation>();
            recipe.m_minStationLevel = 1;
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement { m_resItem = vineberry.GetComponent<ItemDrop>(),  m_amount = 3, m_recover = false },
                new Piece.Requirement { m_resItem = fiddlehead.GetComponent<ItemDrop>(), m_amount = 2, m_recover = false },
                new Piece.Requirement { m_resItem = cloudberry.GetComponent<ItemDrop>(), m_amount = 4, m_recover = false },
            };

            db.m_recipes.Add(recipe);
            Log.Debug("AddVineberryJuiceBaseRecipe: 3 Vineberry + 2 FiddleheadFern + 4 Cloudberries → 1 VineberryJuiceBase");
        }

        private static void AddBlackstoneBrewBaseRecipe(ObjectDB db)
        {
            // Mead Kettle recipe: 2 Obsidian + 4 Pukeberries + 2 Barley → 1 BlackstoneBrewBase
            GameObject obsidian   = db.GetItemPrefab("Obsidian");
            GameObject pukeberries = db.GetItemPrefab("Pukeberries");
            GameObject barley     = db.GetItemPrefab("Barley");
            GameObject cauldron   = ZNetScene.instance?.GetPrefab("piece_MeadCauldron");

            if (obsidian == null || pukeberries == null || barley == null)
            {
                Log.Error("AddBlackstoneBrewBaseRecipe: one or more ingredient prefabs not found " +
                    $"(Obsidian={obsidian != null}, Pukeberries={pukeberries != null}, Barley={barley != null})");
                return;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_BlackstoneBrewBase";
            recipe.m_item = Plugin.BlackstoneBrewBasePrefab.GetComponent<ItemDrop>();
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = cauldron?.GetComponent<CraftingStation>();
            recipe.m_minStationLevel = 1;
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement { m_resItem = obsidian.GetComponent<ItemDrop>(),    m_amount = 2, m_recover = false },
                new Piece.Requirement { m_resItem = pukeberries.GetComponent<ItemDrop>(), m_amount = 4, m_recover = false },
                new Piece.Requirement { m_resItem = barley.GetComponent<ItemDrop>(),      m_amount = 2, m_recover = false },
            };

            db.m_recipes.Add(recipe);
            Log.Debug("AddBlackstoneBrewBaseRecipe: 2 Obsidian + 4 Pukeberries + 2 Barley → 1 BlackstoneBrewBase");
        }

        private static void AddHeftyStoneRecipe(ObjectDB db)
        {
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_HeftyStone";
            recipe.m_item = Plugin.HeftyStonePrefab.GetComponent<ItemDrop>();
            recipe.m_amount = 10;
            recipe.m_enabled = true;
            recipe.m_craftingStation = null;
            recipe.m_minStationLevel = 1;

            GameObject stonePrefab = ObjectDB.instance.GetItemPrefab("Stone");
            if (stonePrefab != null)
            {
                recipe.m_resources = new Piece.Requirement[]
                {
                    new Piece.Requirement
                    {
                        m_resItem = stonePrefab.GetComponent<ItemDrop>(),
                        m_amount = 5,
                        m_amountPerLevel = 0,
                        m_recover = true
                    }
                };
                Log.Debug("AddHeftyStoneRecipe: 5x Stone → 10x Hefty Stone");
            }
            else
            {
                Log.Error("AddHeftyStoneRecipe: could not find Stone prefab");
            }

            db.m_recipes.Add(recipe);
        }

        private static void AddDokkbladRecipe(ObjectDB db)
        {
            GameObject obsidianPrefab    = db.GetItemPrefab("Obsidian");
            GameObject freezeGlandPrefab = db.GetItemPrefab("FreezeGland");
            GameObject leatherPrefab     = db.GetItemPrefab("LeatherScraps");
            GameObject workbenchPrefab   = ZNetScene.instance?.GetPrefab("piece_workbench");

            if (obsidianPrefab == null || freezeGlandPrefab == null)
            {
                Log.Error("AddDokkbladRecipe: one or more ingredient prefabs not found " +
                    $"(Obsidian={obsidianPrefab != null}, FreezeGland={freezeGlandPrefab != null})");
                return;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name              = "Recipe_Dokkblad";
            recipe.m_item            = Plugin.DokkbladPrefab.GetComponent<ItemDrop>();
            recipe.m_amount          = 1;
            recipe.m_enabled         = true;
            recipe.m_craftingStation = workbenchPrefab?.GetComponent<CraftingStation>();
            recipe.m_minStationLevel = 2;
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement { m_resItem = obsidianPrefab.GetComponent<ItemDrop>(),    m_amount = 45, m_amountPerLevel = 0,  m_recover = true },
                new Piece.Requirement { m_resItem = freezeGlandPrefab.GetComponent<ItemDrop>(), m_amount = 10, m_amountPerLevel = 15, m_recover = true },
                new Piece.Requirement { m_resItem = leatherPrefab.GetComponent<ItemDrop>(),     m_amount = 3,  m_amountPerLevel = 0, m_recover = true },
            };

            db.m_recipes.Add(recipe);
            Log.Debug("AddDokkbladRecipe: registered");
        }

        private static void AddKaldmorkRecipe(ObjectDB db)
        {
            GameObject obsidianPrefab    = db.GetItemPrefab("Obsidian");
            GameObject freezeGlandPrefab = db.GetItemPrefab("FreezeGland");
            GameObject leatherPrefab     = db.GetItemPrefab("LeatherScraps");
            GameObject workbenchPrefab   = ZNetScene.instance?.GetPrefab("piece_workbench");

            if (obsidianPrefab == null || freezeGlandPrefab == null || leatherPrefab == null)
            {
                Log.Error("AddKaldmorkRecipe: one or more ingredient prefabs not found " +
                    $"(Obsidian={obsidianPrefab != null}, FreezeGland={freezeGlandPrefab != null}, LeatherScraps={leatherPrefab != null})");
                return;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name             = "Recipe_Kaldmork";
            recipe.m_item           = Plugin.KaldmorkPrefab.GetComponent<ItemDrop>();
            recipe.m_amount         = 1;
            recipe.m_enabled        = true;
            recipe.m_craftingStation = workbenchPrefab?.GetComponent<CraftingStation>();
            recipe.m_minStationLevel = 2;
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement { m_resItem = obsidianPrefab.GetComponent<ItemDrop>(),    m_amount = 15, m_amountPerLevel = 0, m_recover = true },
                new Piece.Requirement { m_resItem = freezeGlandPrefab.GetComponent<ItemDrop>(), m_amount = 5,  m_amountPerLevel = 10, m_recover = true },
                new Piece.Requirement { m_resItem = leatherPrefab.GetComponent<ItemDrop>(),     m_amount = 3,  m_amountPerLevel = 0, m_recover = true },
            };

            db.m_recipes.Add(recipe);
            Log.Debug("AddKaldmorkRecipe: registered");
        }

        private static void AddRootAtgeirRecipe(ObjectDB db)
        {
            AddVineWeaponRecipe(
                db,
                "Recipe_RootAtgeir",
                Plugin.RootAtgeirPrefab,
                bindsinewAmt: 12,
                bindsinewPerLevel: 6,
                barkAmt: 10,
                rootAmt: 6,
                oozeAmt: 6,
                oozePerLevel: 3,
                ironAmt: 5);
        }

        private static void AddRootSpearRecipe(ObjectDB db)
        {
            AddVineWeaponRecipe(
                db,
                "Recipe_RootSpear",
                Plugin.RootSpearPrefab,
                bindsinewAmt: 10,
                bindsinewPerLevel: 5,
                barkAmt: 8,
                rootAmt: 5,
                oozeAmt: 4,
                oozePerLevel: 2,
                ironAmt: 3);
        }

        // Shared Swamp-tier vine-weapon recipe (workbench L2). Bindsinew (the cultivated
        // vine fiber) is the signature ingredient and carries the per-level scaling;
        // Ooze (poison goo) also scales, tying upgrades to the poison identity. Ancient
        // Bark is the organic haft, Root the swampy binding, Iron the tool's metal
        // (initial cost only — upgrades stay organic).
        private static void AddVineWeaponRecipe(
            ObjectDB db,
            string recipeName,
            GameObject itemPrefab,
            int bindsinewAmt,
            int bindsinewPerLevel,
            int barkAmt,
            int rootAmt,
            int oozeAmt,
            int oozePerLevel,
            int ironAmt)
        {
            GameObject bindsinew = Plugin.BindsinewPrefab;
            GameObject bark      = db.GetItemPrefab("ElderBark"); // "Ancient bark" — internal ID is ElderBark
            GameObject root      = db.GetItemPrefab("Root");
            GameObject ooze      = db.GetItemPrefab("Ooze");
            GameObject iron      = db.GetItemPrefab("Iron");
            GameObject workbench = ZNetScene.instance?.GetPrefab("piece_workbench");

            if (bindsinew == null || bark == null || root == null || ooze == null || iron == null)
            {
                Log.Error($"{recipeName}: one or more ingredient prefabs not found " +
                    $"(Bindsinew={bindsinew != null}, ElderBark={bark != null}, Root={root != null}, Ooze={ooze != null}, Iron={iron != null})");
                return;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name              = recipeName;
            recipe.m_item            = itemPrefab.GetComponent<ItemDrop>();
            recipe.m_amount          = 1;
            recipe.m_enabled         = true;
            recipe.m_craftingStation = workbench?.GetComponent<CraftingStation>();
            recipe.m_minStationLevel = 2;
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement { m_resItem = bindsinew.GetComponent<ItemDrop>(), m_amount = bindsinewAmt, m_amountPerLevel = bindsinewPerLevel, m_recover = true },
                new Piece.Requirement { m_resItem = bark.GetComponent<ItemDrop>(),      m_amount = barkAmt,      m_amountPerLevel = 0,            m_recover = true },
                new Piece.Requirement { m_resItem = iron.GetComponent<ItemDrop>(),      m_amount = ironAmt,      m_amountPerLevel = 0,            m_recover = true },
                new Piece.Requirement { m_resItem = root.GetComponent<ItemDrop>(),      m_amount = rootAmt,      m_amountPerLevel = 0,            m_recover = true },
                new Piece.Requirement { m_resItem = ooze.GetComponent<ItemDrop>(),      m_amount = oozeAmt,      m_amountPerLevel = oozePerLevel, m_recover = true },
            };

            db.m_recipes.Add(recipe);
            Log.Debug($"{recipeName}: registered (Bindsinew signature ingredient)");
        }

        private static void AddSmoothStoneRecipe(ObjectDB db)
        {
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_SmoothStone";
            recipe.m_item = Plugin.SmoothStonePrefab.GetComponent<ItemDrop>();
            recipe.m_amount = 10;
            recipe.m_enabled = true;
            recipe.m_craftingStation = null;
            recipe.m_minStationLevel = 1;

            GameObject flintPrefab = ObjectDB.instance.GetItemPrefab("Flint");
            if (flintPrefab != null)
            {
                recipe.m_resources = new Piece.Requirement[]
                {
                    new Piece.Requirement
                    {
                        m_resItem = flintPrefab.GetComponent<ItemDrop>(),
                        m_amount = 3,
                        m_amountPerLevel = 0,
                        m_recover = true
                    }
                };
                Log.Debug("AddSmoothStoneRecipe: 3x Flint → 10x Smooth Stone");
            }
            else
            {
                Log.Error("AddSmoothStoneRecipe: could not find Flint prefab");
            }

            db.m_recipes.Add(recipe);
        }
    }
}
