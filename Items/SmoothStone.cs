using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    public static class SmoothStone
    {
        public static GameObject ProjectilePrefab { get; private set; }

        private static Transform _meshTransform;
        private static Vector3 _flintBaseScale;

        public static GameObject CreatePrefab()
        {
            Log.Debug("SmoothStone.CreatePrefab: starting");

            GameObject clubPrefab = ZNetScene.instance.GetPrefab("Club");
            if (clubPrefab == null)
            {
                Log.Error("SmoothStone.CreatePrefab: could not find Club prefab");
                return null;
            }

            GameObject flintPrefab = ZNetScene.instance.GetPrefab("Flint");
            if (flintPrefab == null)
            {
                Log.Error("SmoothStone.CreatePrefab: could not find Flint prefab");
                return null;
            }

            GameObject greydwarfProjectile = ZNetScene.instance.GetPrefab("Greydwarf_throw_projectile");
            if (greydwarfProjectile == null)
            {
                Log.Error("SmoothStone.CreatePrefab: could not find Greydwarf_throw_projectile prefab");
                return null;
            }

            GameObject prefab = Object.Instantiate(clubPrefab, Plugin.PrefabContainer);
            prefab.name = "SmoothStone";
            Log.Debug("SmoothStone.CreatePrefab: cloned Club under inactive container");

            SwapMesh(prefab, flintPrefab);
            ProjectilePrefab = CreateProjectilePrefab(prefab, greydwarfProjectile);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                Log.Error("SmoothStone.CreatePrefab: cloned prefab has no ItemDrop component");
                return null;
            }

            ItemDrop flintItemDrop = flintPrefab.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;

            // Basic item info
            shared.m_name = "$item_smoothstone";
            shared.m_description = "$item_smoothstone_desc";
            shared.m_itemType = ItemDrop.ItemData.ItemType.OneHandedWeapon;
            shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
            shared.m_skillType = RockerySkill.SkillType;
            shared.m_attachOverride = ItemDrop.ItemData.ItemType.Tool;
            Log.Debug("SmoothStone.CreatePrefab: set basic item info");

            // Copy icon from Flint, shrunk so it reads as a smaller variant in the inventory slot
            if (flintItemDrop != null && flintItemDrop.m_itemData.m_shared.m_icons != null)
            {
                shared.m_icons = VisualUtil.ShrinkIcons(flintItemDrop.m_itemData.m_shared.m_icons, 0.7f);
                Log.Debug($"SmoothStone.CreatePrefab: copied {shared.m_icons.Length} icon(s) from Flint (shrunk 0.7x)");
            }
            else
            {
                Log.Warn("SmoothStone.CreatePrefab: could not copy icons from Flint");
            }

            // Stats — lighter, sharper, better thrown, weaker melee
            shared.m_damages = new HitData.DamageTypes
            {
                m_blunt = 5f,
                m_pierce = 2f
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes();
            shared.m_attackForce = 15f;
            shared.m_backstabBonus = 4f;
            shared.m_useDurability = false;
            shared.m_weight = 0.3f;
            shared.m_blockPower = 2f;
            shared.m_maxQuality = 1;
            shared.m_maxStackSize = 50;
            Log.Debug("SmoothStone.CreatePrefab: set weapon stats");

            // Primary attack — punch
            if (shared.m_attack == null)
                shared.m_attack = new Attack();
            shared.m_attack.m_attackType = Attack.AttackType.Horizontal;
            shared.m_attack.m_attackAnimation = "unarmed_attack";
            shared.m_attack.m_attackRange = 1.5f;
            shared.m_attack.m_attackChainLevels = 2;
            shared.m_attack.m_attackProjectile = null;
            Log.Debug("SmoothStone.CreatePrefab: configured primary attack (unarmed)");

            // Secondary attack — throw (consumed, faster and more accurate than Hefty Stone)
            if (shared.m_secondaryAttack == null)
                shared.m_secondaryAttack = new Attack();
            shared.m_secondaryAttack.m_attackType = Attack.AttackType.Projectile;
            shared.m_secondaryAttack.m_attackAnimation = "spear_throw";
            shared.m_secondaryAttack.m_attackProjectile = ProjectilePrefab ?? greydwarfProjectile;
            shared.m_secondaryAttack.m_projectileVel = 30f;
            shared.m_secondaryAttack.m_projectileVelMin = 20f;
            shared.m_secondaryAttack.m_projectileAccuracy = 1f;
            shared.m_secondaryAttack.m_consumeItem = true;
            shared.m_secondaryAttack.m_attackHeight = 1.2f;
            shared.m_secondaryAttack.m_attackRange = 1.0f;
            shared.m_secondaryAttack.m_launchAngle = 0f;
            shared.m_secondaryAttack.m_damageMultiplier = 1.5f;
            Log.Debug("SmoothStone.CreatePrefab: configured secondary attack (throw)");

            Log.Debug("SmoothStone.CreatePrefab: complete");
            return prefab;
        }

        public static void ApplyMeshTransforms(GameObject prefab)
        {
            if (_meshTransform == null)
                return;

#if DEBUG
            _meshTransform.localPosition = new Vector3(
                Plugin.HeldOffsetX.Value, Plugin.HeldOffsetY.Value, Plugin.HeldOffsetZ.Value);
            _meshTransform.localRotation = Quaternion.Euler(
                Plugin.HeldRotX.Value, Plugin.HeldRotY.Value, Plugin.HeldRotZ.Value);
            _meshTransform.localScale = _flintBaseScale * Plugin.HeldScale.Value;
#else
            _meshTransform.localPosition = new Vector3(HeftyStone.HeldOffsetX, HeftyStone.HeldOffsetY, HeftyStone.HeldOffsetZ);
            _meshTransform.localRotation = Quaternion.Euler(HeftyStone.HeldRotX, HeftyStone.HeldRotY, HeftyStone.HeldRotZ);
            _meshTransform.localScale = _flintBaseScale * HeftyStone.HeldScale;
#endif
        }

        private static GameObject CreateProjectilePrefab(GameObject weaponPrefab, GameObject baseProjectile)
        {
            GameObject projPrefab = Object.Instantiate(baseProjectile, Plugin.PrefabContainer);
            projPrefab.name = "SmoothStone_projectile";

            Transform attach = weaponPrefab.transform.Find("attach");
            if (attach != null)
            {
                if (VisualUtil.CopyMeshInto(projPrefab, attach.gameObject))
                {
                    // Match the weapon's displayed mesh scale so the in-flight stone reads the same size as the held one.
                    MeshFilter targetMF = projPrefab.GetComponentInChildren<MeshFilter>();
                    if (targetMF != null && _meshTransform != null)
                        targetMF.transform.localScale = _meshTransform.localScale;
                }
                else
                {
                    Log.Warn("SmoothStone: CopyMeshInto projectile failed");
                }
            }
            else
            {
                Log.Warn("SmoothStone: no 'attach' node on weapon — projectile will use default mesh");
            }

            return projPrefab;
        }

        private static void SwapMesh(GameObject weaponPrefab, GameObject flintPrefab)
        {
            MeshFilter flintMeshFilter = flintPrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer flintMeshRenderer = flintPrefab.GetComponentInChildren<MeshRenderer>();

            if (flintMeshFilter == null || flintMeshRenderer == null)
            {
                Log.Warn("SmoothStone.SwapMesh: Flint mesh components missing");
                return;
            }

            _flintBaseScale = flintMeshFilter.transform.localScale;
            Log.Debug($"SmoothStone.SwapMesh: found Flint mesh '{flintMeshFilter.sharedMesh?.name}', baseScale={_flintBaseScale}");

            Transform attach = weaponPrefab.transform.Find("attach");
            if (attach == null)
            {
                Log.Warn("SmoothStone.SwapMesh: no 'attach' child found");
                return;
            }
            attach.localRotation = Quaternion.Euler(0f, 90f, 0f);
            attach.localPosition = Vector3.zero;

            MeshFilter mf = attach.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = attach.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null)
            {
                Log.Warn("SmoothStone.SwapMesh: no MeshFilter/MeshRenderer in 'attach'");
                return;
            }

            mf.sharedMesh = flintMeshFilter.sharedMesh;
            mr.sharedMaterials = flintMeshRenderer.sharedMaterials;
            _meshTransform = mf.transform;
            Log.Debug("SmoothStone.SwapMesh: mesh swapped in 'attach'");

            ApplyMeshTransforms(weaponPrefab);
        }
    }
}
