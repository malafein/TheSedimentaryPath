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
                __instance.m_StatusEffects.Add(stoneKinSE);
                Log.Debug("ObjectDB.Awake: SE_StoneKin registered");
            }

            // Build KinFist (hidden ItemData, not in inventory).
            // Tier-3 KinFist activation reads its damage from this
            // shared instance via Humanoid.GetCurrentWeapon postfix.
            KinFist.Build();

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
            recipe.m_minStationLevel = 3;
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
