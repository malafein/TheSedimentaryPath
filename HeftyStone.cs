using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class HeftyStone
    {
        // Reference to the mesh transform so we can re-apply config changes
        private static Transform _meshTransform;
        private static Vector3 _stoneBaseScale;

        public static GameObject CreatePrefab()
        {
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: starting");

            GameObject clubPrefab = ZNetScene.instance.GetPrefab("Club");
            if (clubPrefab == null)
            {
                ZLog.LogError("[TheSedimentaryPath] CreatePrefab: Could not find Club prefab");
                return null;
            }
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: found Club prefab");

            GameObject stonePrefab = ZNetScene.instance.GetPrefab("Stone");
            if (stonePrefab == null)
            {
                ZLog.LogError("[TheSedimentaryPath] CreatePrefab: Could not find Stone prefab");
                return null;
            }
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: found Stone prefab");

            GameObject greydwarfProjectile = ZNetScene.instance.GetPrefab("Greydwarf_throw_projectile");
            if (greydwarfProjectile == null)
            {
                ZLog.LogError("[TheSedimentaryPath] CreatePrefab: Could not find Greydwarf_throw_projectile prefab");
                return null;
            }
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: found Greydwarf_throw_projectile prefab");

            // Clone the Club under an inactive parent container.
            // This gives us activeSelf=true but activeInHierarchy=false:
            // - ZNetView.Awake() does NOT fire (no NRE spam)
            // - VisEquipment CAN read the mesh hierarchy (activeSelf is true)
            // This is the same pattern used by Jotunn and ValheimLib.
            GameObject prefab = Object.Instantiate(clubPrefab, Plugin.PrefabContainer);
            prefab.name = "HeftyStone";
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: cloned Club under inactive container");

            // Swap the visual mesh to look like a stone
            SwapMesh(prefab, stonePrefab);

            // Configure weapon stats
            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                ZLog.LogError("[TheSedimentaryPath] CreatePrefab: cloned prefab has no ItemDrop component");
                return null;
            }

            ItemDrop stoneItemDrop = stonePrefab.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;

            // Basic item info
            shared.m_name = "$item_heftystone";
            shared.m_description = "$item_heftystone_desc";
            shared.m_itemType = ItemDrop.ItemData.ItemType.OneHandedWeapon;
            shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
            shared.m_skillType = RockerySkill.SkillType;
            shared.m_attachOverride = ItemDrop.ItemData.ItemType.Tool; // sheath on hip
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: set basic item info");

            // Copy icon from Stone, shrunk so it reads as a smaller variant in the inventory slot
            if (stoneItemDrop != null && stoneItemDrop.m_itemData.m_shared.m_icons != null)
            {
                shared.m_icons = VisualUtil.ShrinkIcons(stoneItemDrop.m_itemData.m_shared.m_icons, 0.7f);
                ZLog.Log($"[TheSedimentaryPath] CreatePrefab: copied {shared.m_icons.Length} icon(s) from Stone (shrunk 0.7x)");
            }
            else
            {
                ZLog.LogWarning("[TheSedimentaryPath] CreatePrefab: could not copy icons from Stone");
            }

            // Stats
            shared.m_damages = new HitData.DamageTypes
            {
                m_blunt = 8f
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes();
            shared.m_attackForce = 30f;
            shared.m_backstabBonus = 3f;
            shared.m_maxDurability = 100f;
            shared.m_durabilityPerLevel = 0f;
            shared.m_weight = 0.5f;
            shared.m_blockPower = 5f;
            shared.m_maxQuality = 1;
            shared.m_maxStackSize = 50;
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: set weapon stats");

            // Primary attack — punch
            if (shared.m_attack == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] CreatePrefab: m_attack is null, creating new Attack");
                shared.m_attack = new Attack();
            }
            shared.m_attack.m_attackType = Attack.AttackType.Horizontal;
            shared.m_attack.m_attackAnimation = "unarmed_attack";
            shared.m_attack.m_attackRange = 1.5f;
            shared.m_attack.m_attackChainLevels = 2;
            shared.m_attack.m_attackProjectile = null;
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: configured primary attack (unarmed)");

            // Secondary attack — throw (consumed)
            if (shared.m_secondaryAttack == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] CreatePrefab: m_secondaryAttack is null, creating new Attack");
                shared.m_secondaryAttack = new Attack();
            }
            shared.m_secondaryAttack.m_attackType = Attack.AttackType.Projectile;
            shared.m_secondaryAttack.m_attackAnimation = "spear_throw";
            shared.m_secondaryAttack.m_attackProjectile = greydwarfProjectile;
            shared.m_secondaryAttack.m_projectileVel = 25f;
            shared.m_secondaryAttack.m_projectileVelMin = 15f;
            shared.m_secondaryAttack.m_projectileAccuracy = 2f;
            shared.m_secondaryAttack.m_consumeItem = true;
            shared.m_secondaryAttack.m_attackHeight = 1.2f;
            shared.m_secondaryAttack.m_attackRange = 1.0f;
            shared.m_secondaryAttack.m_launchAngle = 0f;
            ZLog.Log("[TheSedimentaryPath] CreatePrefab: configured secondary attack (throw)");

            ZLog.Log("[TheSedimentaryPath] CreatePrefab: complete");
            return prefab;
        }

        /// <summary>
        /// Re-applies mesh transforms from current config values.
        /// Called on initial setup and when config values change.
        /// </summary>
        public static void ApplyMeshTransforms(GameObject prefab)
        {
            if (_meshTransform == null)
                return;

            _meshTransform.localPosition = new Vector3(
                Plugin.HeldOffsetX.Value, Plugin.HeldOffsetY.Value, Plugin.HeldOffsetZ.Value);
            _meshTransform.localRotation = Quaternion.Euler(
                Plugin.HeldRotX.Value, Plugin.HeldRotY.Value, Plugin.HeldRotZ.Value);
            _meshTransform.localScale = _stoneBaseScale * Plugin.HeldScale.Value;
            ZLog.Log($"[TheSedimentaryPath] ApplyMeshTransforms: pos={_meshTransform.localPosition}, rot={_meshTransform.localEulerAngles}, scale={Plugin.HeldScale.Value}");
        }

        private static void SwapMesh(GameObject weaponPrefab, GameObject stonePrefab)
        {
            MeshFilter stoneMeshFilter = stonePrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer stoneMeshRenderer = stonePrefab.GetComponentInChildren<MeshRenderer>();

            if (stoneMeshFilter == null || stoneMeshRenderer == null)
            {
                ZLog.LogWarning($"[TheSedimentaryPath] SwapMesh: Stone mesh components missing (filter={stoneMeshFilter != null}, renderer={stoneMeshRenderer != null})");
                return;
            }

            _stoneBaseScale = stoneMeshFilter.transform.localScale;
            ZLog.Log($"[TheSedimentaryPath] SwapMesh: found Stone mesh '{stoneMeshFilter.sharedMesh?.name}' with {stoneMeshRenderer.sharedMaterials?.Length ?? 0} material(s), baseScale={_stoneBaseScale}");

            // Set the attach node rotation to match StaffSkeleton's pattern (90 deg Y rotation)
            Transform attach = weaponPrefab.transform.Find("attach");
            if (attach == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] SwapMesh: no 'attach' child found");
                return;
            }
            attach.localRotation = Quaternion.Euler(0f, 90f, 0f);
            attach.localPosition = Vector3.zero;
            ZLog.Log("[TheSedimentaryPath] SwapMesh: set attach node rot=(0, 90, 0) pos=(0, 0, 0)");

            // Find and swap the mesh in the attach child
            MeshFilter mf = attach.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = attach.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] SwapMesh: no MeshFilter/MeshRenderer in 'attach'");
                return;
            }

            mf.sharedMesh = stoneMeshFilter.sharedMesh;
            mr.sharedMaterials = stoneMeshRenderer.sharedMaterials;
            _meshTransform = mf.transform;
            ZLog.Log("[TheSedimentaryPath] SwapMesh: mesh swapped in 'attach'");

            // Apply initial transforms from config
            ApplyMeshTransforms(weaponPrefab);
        }

    }
}
