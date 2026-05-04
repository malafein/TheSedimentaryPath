using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDBPatch
    {
        private static readonly FieldInfo ItemByHashField =
            AccessTools.Field(typeof(ObjectDB), "m_itemByHash");

        public static void Postfix(ObjectDB __instance)
        {
            ZLog.Log($"[TheSedimentaryPath] ObjectDB.Awake postfix: m_items.Count={__instance.m_items.Count}");

            // Reset the Wishbone effect cache so it re-fetches from this ObjectDB instance.
            WishboneEffects.Reset();

            if (__instance.m_items.Count == 0)
            {
                ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: skipping (title screen, no items)");
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
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: Hefty Stone fully registered");
                }
                else
                {
                    ZLog.LogError("[TheSedimentaryPath] ObjectDB.Awake: HeftyStone.CreatePrefab returned null");
                }
            }
            else
            {
                ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: HeftyStone already registered, skipping");
            }

            // Register Blackstone Brew (base + fermented) + SE
            if (__instance.GetItemPrefab("BlackstoneBrew") == null)
            {
                StatusEffect brewSE = BlackstoneBrew.CreateStatusEffect();
                if (brewSE != null)
                {
                    __instance.m_StatusEffects.Add(brewSE);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: SE_BlackstoneBrew registered");
                }

                GameObject basePrefab = BlackstoneBrew.CreateBasePrefab();
                if (basePrefab != null)
                {
                    Plugin.BlackstoneBrewBasePrefab = basePrefab;
                    RegisterItem(__instance, basePrefab);
                    RegisterInZNetScene(basePrefab);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: BlackstoneBrewBase registered");
                }

                GameObject brewPrefab = BlackstoneBrew.CreateBrewPrefab(brewSE);
                if (brewPrefab != null)
                {
                    Plugin.BlackstoneBrewPrefab = brewPrefab;
                    RegisterItem(__instance, brewPrefab);
                    RegisterInZNetScene(brewPrefab);
                    AddBlackstoneBrewBaseRecipe(__instance);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: BlackstoneBrew registered");
                }
            }
            else
            {
                ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: BlackstoneBrew already registered, skipping");
            }

            // Register Vineberry Juice (base + fermented) + SE
            if (__instance.GetItemPrefab("VineberryJuice") == null)
            {
                StatusEffect juiceSE = VineberryJuice.CreateStatusEffect();
                if (juiceSE != null)
                {
                    __instance.m_StatusEffects.Add(juiceSE);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: SE_VineberryJuice registered");
                }

                GameObject juiceBasePrefab = VineberryJuice.CreateBasePrefab();
                if (juiceBasePrefab != null)
                {
                    Plugin.VineberryJuiceBasePrefab = juiceBasePrefab;
                    RegisterItem(__instance, juiceBasePrefab);
                    RegisterInZNetScene(juiceBasePrefab);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: VineberryJuiceBase registered");
                }

                GameObject juicePrefab = VineberryJuice.CreateJuicePrefab(juiceSE);
                if (juicePrefab != null)
                {
                    Plugin.VineberryJuicePrefab = juicePrefab;
                    RegisterItem(__instance, juicePrefab);
                    RegisterInZNetScene(juicePrefab);
                    AddVineberryJuiceBaseRecipe(__instance);
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: VineberryJuice registered");
                }
            }
            else
            {
                ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: VineberryJuice already registered, skipping");
            }

            // Register the stance status effect once (shared by all stance weapons)
            if (!__instance.m_StatusEffects.Exists(se => se is SE_WeaponStance))
            {
                SE_WeaponStance stanceSE = ScriptableObject.CreateInstance<SE_WeaponStance>();
                stanceSE.name  = "SE_WeaponStance";
                stanceSE.m_name = "$se_weaponstance";
                stanceSE.m_ttl  = 3600f;
                __instance.m_StatusEffects.Add(stanceSE);
                Plugin.DebugLog("ObjectDB.Awake: SE_WeaponStance registered");
            }

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

                    Plugin.StanceWeapons["$item_kaldmork"] = kaldmorkWeapon;
                    AddKaldmorkRecipe(__instance);
                    Plugin.DebugLog("ObjectDB.Awake: obsidian dagger fully registered");
                }
                else
                {
                    ZLog.LogError("[TheSedimentaryPath] ObjectDB.Awake: ObsidianDagger.CreatePrefab returned null");
                }
            }
            else
            {
                Plugin.DebugLog("ObjectDB.Awake: obsidian dagger already registered, skipping");
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
                    ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: Smooth Stone fully registered");
                }
                else
                {
                    ZLog.LogError("[TheSedimentaryPath] ObjectDB.Awake: SmoothStone.CreatePrefab returned null");
                }
            }
            else
            {
                ZLog.Log("[TheSedimentaryPath] ObjectDB.Awake: SmoothStone already registered, skipping");
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
                ZLog.Log($"[TheSedimentaryPath] RegisterItem: registered {prefab.name} (hash={hash})");
            }
            else
            {
                ZLog.LogError($"[TheSedimentaryPath] RegisterItem: m_itemByHash field not found for {prefab.name}");
            }
        }

        private static void RegisterInZNetScene(GameObject prefab)
        {
            if (ZNetScene.instance == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] RegisterInZNetScene: ZNetScene.instance is null");
                return;
            }

            FieldInfo field = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            if (field == null)
            {
                ZLog.LogError("[TheSedimentaryPath] RegisterInZNetScene: m_namedPrefabs field not found");
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
                    ZLog.Log($"[TheSedimentaryPath] RegisterInZNetScene: registered prefab with ZNetView (hash={hash})");
                }
                else
                {
                    ZLog.Log($"[TheSedimentaryPath] RegisterInZNetScene: registered prefab without ZNetView (hash={hash})");
                }
            }
            else
            {
                ZLog.Log("[TheSedimentaryPath] RegisterInZNetScene: already registered");
            }
        }

        private static void AddLocalization()
        {
            Localization loc = Localization.instance;
            if (loc == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] AddLocalization: Localization.instance is null");
                return;
            }

            FieldInfo field = AccessTools.Field(typeof(Localization), "m_translations");
            if (field == null)
            {
                ZLog.LogError("[TheSedimentaryPath] AddLocalization: m_translations field not found");
                return;
            }

            var translations = (Dictionary<string, string>)field.GetValue(loc);
            translations["item_heftystone"] = "Hefty Stone";
            translations["item_heftystone_desc"] = "A carefully chosen stone, dense and well-balanced. Perfect for throwing \u2014 or for making a point.";
            translations["item_smoothstone"] = "Smooth Stone";
            translations["item_smoothstone_desc"] = "A flat, water-worn stone that fits perfectly between the fingers. Aerodynamic and precise.";
            translations["item_vineberryjuicebase"]       = "Vineberry Juice Base";
            translations["item_vineberryjuicebase_desc"]  = "A blend of vineberry, fiddlehead sprouts, and cloudberries. Sweet, strange, and not yet finished. The fermenter will decide the rest.";
            translations["item_vineberryjuice"]           = "Vineberry Juice";
            translations["item_vineberryjuice_desc"]      = "Sweet, still, and faintly luminous. The taste of long afternoons. Cloudberry bright. Vineberry deep. Fiddlehead strange \u2014 something in the blend knows you.";
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
            translations["se_weaponstance"]       = "Weapon Stance";
            translations[$"skill_{RockerySkill.SkillId}"]             = "Rockery";
            translations[$"skill_{RockerySkill.SkillId}_description"]  = "Stone speaks to those who listen.";
            translations["skill_rockery_desc"]                          = "Stone speaks to those who listen.";
            translations[$"skill_{VinerySkill.SkillId}"]               = "Vinery";
            translations[$"skill_{VinerySkill.SkillId}_description"]   = "The patient know: a watched vine does not wither.";
            translations["skill_vinery_desc"]                          = "The patient know: a watched vine does not wither.";
            ZLog.Log($"[TheSedimentaryPath] AddLocalization: added {8} translation(s), total translations={translations.Count}");
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
                ZLog.LogError("[TheSedimentaryPath] AddVineberryJuiceBaseRecipe: one or more ingredient prefabs not found " +
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
            ZLog.Log("[TheSedimentaryPath] AddVineberryJuiceBaseRecipe: 3 Vineberry + 2 FiddleheadFern + 4 Cloudberries → 1 VineberryJuiceBase");
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
                ZLog.LogError("[TheSedimentaryPath] AddBlackstoneBrewBaseRecipe: one or more ingredient prefabs not found " +
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
            ZLog.Log("[TheSedimentaryPath] AddBlackstoneBrewBaseRecipe: 2 Obsidian + 4 Pukeberries + 2 Barley → 1 BlackstoneBrewBase");
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
                ZLog.Log("[TheSedimentaryPath] AddHeftyStoneRecipe: 5x Stone -> 10x Hefty Stone");
            }
            else
            {
                ZLog.LogError("[TheSedimentaryPath] AddHeftyStoneRecipe: could not find Stone prefab");
            }

            db.m_recipes.Add(recipe);
        }

        private static void AddKaldmorkRecipe(ObjectDB db)
        {
            GameObject obsidianPrefab    = db.GetItemPrefab("Obsidian");
            GameObject freezeGlandPrefab = db.GetItemPrefab("FreezeGland");
            GameObject leatherPrefab     = db.GetItemPrefab("LeatherScraps");
            GameObject workbenchPrefab   = ZNetScene.instance?.GetPrefab("piece_workbench");

            if (obsidianPrefab == null || freezeGlandPrefab == null || leatherPrefab == null)
            {
                ZLog.LogError("[TheSedimentaryPath] AddKaldmorkRecipe: one or more ingredient prefabs not found " +
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
            Plugin.DebugLog("AddKaldmorkRecipe: registered");
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
                ZLog.Log("[TheSedimentaryPath] AddSmoothStoneRecipe: 3x Flint -> 10x Smooth Stone");
            }
            else
            {
                ZLog.LogError("[TheSedimentaryPath] AddSmoothStoneRecipe: could not find Flint prefab");
            }

            db.m_recipes.Add(recipe);
        }
    }
}
