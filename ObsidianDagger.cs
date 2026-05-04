using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public class ObsidianDagger : IStanceWeapon
    {
        // Client-side stance flag. Swapping m_secondaryAttack on the shared data is safe
        // because SharedData is per-item-type and the server only receives resulting HitData,
        // not which secondary attack definition was used.
        public bool IsThrowStance { get; private set; } = true;

        public GameObject ProjectilePrefab { get; private set; }

        private ItemDrop.ItemData.SharedData _shared;
        private Attack _throwAttack;
        private Attack _meleeSecondary;

        // Deep purple-black for obsidian; chitin's green base texture needs a strong bias away from green.
        private static readonly Color ObsidianMaterialTint = new Color(0.04f, 0.02f, 0.07f, 1f);
        // Very dark blue-black so the icon reads as volcanic glass; keep slight hue so it's not a pure black void.
        private static readonly Color ObsidianIconTint = new Color(0.08f, 0.07f, 0.12f, 1f);

        public GameObject CreatePrefab()
        {
            GameObject knifeChitinPrefab = ZNetScene.instance.GetPrefab("KnifeChitin");
            if (knifeChitinPrefab == null)
            {
                ZLog.LogError("[TheSedimentaryPath] ObsidianDagger.CreatePrefab: KnifeChitin not found");
                return null;
            }

            GameObject prefab = Object.Instantiate(knifeChitinPrefab, Plugin.PrefabContainer);
            prefab.name = "Kaldmork";

            VisualUtil.TintMaterials(prefab, ObsidianMaterialTint);
            VisualUtil.ZeroEmission(prefab);

            ProjectilePrefab = CreateProjectilePrefab(prefab);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                ZLog.LogError("[TheSedimentaryPath] ObsidianDagger.CreatePrefab: no ItemDrop on clone");
                return null;
            }

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            _shared = shared;

            shared.m_name        = "$item_kaldmork";
            shared.m_description = "$item_kaldmork_desc";
            shared.m_skillType   = Skills.SkillType.Knives;

            shared.m_icons = VisualUtil.TintIcons(shared.m_icons, ObsidianIconTint);

            // Ambient cold effect — vfx_Cold is the particle effect applied to characters in cold biomes.
            // Scaled down and attached to the weapon's grip node so it drifts off the blade while held or dropped.
            GameObject vfxColdPrefab = ZNetScene.instance?.GetPrefab("vfx_Cold");
            Transform attachNode = prefab.transform.Find("attach");
            if (vfxColdPrefab != null && attachNode != null)
            {
                GameObject ambientVfx = Object.Instantiate(vfxColdPrefab, attachNode);
                ambientVfx.name = "vfx_frost_ambient";
                ambientVfx.transform.localPosition = new Vector3(0f, 0f, 0.25f);
                ambientVfx.transform.localRotation = Quaternion.identity;
                ambientVfx.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                foreach (var ps in ambientVfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = true;
                    main.playOnAwake = true;
                    ps.Play();
                }
            }
            else
            {
                ZLog.LogWarning("[TheSedimentaryPath] ObsidianDagger: vfx_Cold not found or no attach node — ambient effect skipped");
            }

            // Stats sit between Abyssal Razor and Silver Knife; frost replaces spirit.
            shared.m_damages = new HitData.DamageTypes
            {
                m_pierce = 23f,
                m_slash  = 23f,
                m_frost  = 14f,
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes
            {
                m_pierce = 0f,
                m_slash  = 0f,
                m_frost  = 2f,
            };
            shared.m_attackForce        = 20f;
            shared.m_backstabBonus      = 3f;
            shared.m_maxDurability      = 200f;
            shared.m_durabilityPerLevel = 50f;
            shared.m_weight             = 1.0f;
            shared.m_blockPower         = 15f;
            shared.m_maxQuality         = 4;
            shared.m_maxStackSize       = 1;

            // Preserve the vanilla knife secondary as the leap/melee stance option.
            _meleeSecondary = shared.m_secondaryAttack;

            if (ProjectilePrefab != null)
            {
                _throwAttack = new Attack();
                _throwAttack.m_attackType         = Attack.AttackType.Projectile;
                _throwAttack.m_attackAnimation    = "spear_throw";
                _throwAttack.m_attackProjectile   = ProjectilePrefab;
                _throwAttack.m_projectileVel      = 40f;
                _throwAttack.m_projectileVelMin   = 22f;
                _throwAttack.m_projectileAccuracy = 0.5f;
                _throwAttack.m_consumeItem        = true;
                _throwAttack.m_attackHeight       = 1.3f;
                _throwAttack.m_attackRange        = 1.0f;
                _throwAttack.m_launchAngle        = 0f;
                _throwAttack.m_damageMultiplier   = 1.8f;
            }

            shared.m_secondaryAttack = IsThrowStance ? (_throwAttack ?? _meleeSecondary) : _meleeSecondary;

            return prefab;
        }

        public void ToggleStance()
        {
            IsThrowStance = !IsThrowStance;
            ApplyStance();
        }

        public void ApplyStance()
        {
            string msgKey = IsThrowStance ? "$kaldmork_stance_throw" : "$kaldmork_stance_leap";
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msgKey);

            Player player = Player.m_localPlayer;
            if (player == null) return;

            // Each spawned item may have its own copy of SharedData rather than sharing the prefab
            // reference in _shared, so always write to the player's actual equipped weapon.
            var equippedShared = player.GetCurrentWeapon()?.m_shared;
            if (equippedShared != null)
                equippedShared.m_secondaryAttack = IsThrowStance ? (_throwAttack ?? _meleeSecondary) : _meleeSecondary;

            int seHash = "SE_WeaponStance".GetStableHashCode();
            // AddStatusEffect returns null when the SE is already active — get the existing instance directly.
            var seman = player.GetSEMan();
            var se = (seman.GetStatusEffect(seHash) ?? seman.AddStatusEffect(seHash)) as SE_WeaponStance;
            if (se != null)
            {
                string stanceName = Localization.instance.Localize(msgKey);
                Sprite icon = _shared?.m_icons?.Length > 0 ? _shared.m_icons[0] : null;
                se.Refresh(stanceName, icon);
            }
        }

        private GameObject CreateProjectilePrefab(GameObject weaponPrefab)
        {
            GameObject baseProj = ZNetScene.instance.GetPrefab("Greydwarf_throw_projectile");
            if (baseProj == null)
            {
                ZLog.LogWarning("[TheSedimentaryPath] ObsidianDagger: Greydwarf_throw_projectile not found — throw stance disabled");
                return null;
            }

            GameObject projPrefab = Object.Instantiate(baseProj, Plugin.PrefabContainer);
            projPrefab.name = "Kaldmork_projectile";

            Transform attach = weaponPrefab.transform.Find("attach");
            if (attach != null)
            {
                if (!VisualUtil.CopyMeshInto(projPrefab, attach.gameObject))
                    ZLog.LogWarning("[TheSedimentaryPath] ObsidianDagger: CopyMeshInto projectile failed");
            }
            else
            {
                ZLog.LogWarning("[TheSedimentaryPath] ObsidianDagger: no 'attach' node on weapon — projectile will use default mesh");
            }

            Projectile proj = projPrefab.GetComponent<Projectile>();
            if (proj != null)
            {
                // Behaves like a thrown spear: knife drops at hit location for retrieval.
                proj.m_respawnItemOnHit    = true;
                proj.m_spawnOnTtl          = false; // m_respawnItemOnHit spawns on hit; m_spawnOnTtl would fire SpawnOnHit a second time after stayTTL, duplicating the drop
                proj.m_stayAfterHitStatic  = true;
                proj.m_stayAfterHitDynamic = true;
                proj.m_stayTTL             = 4f;
                proj.m_attachToRigidBody   = true;
                proj.m_attachToClosestBone = true;
                proj.m_ttl                 = 10f;
                proj.m_gravity             = 3f;
                proj.m_hitNoise            = 20f;
                proj.m_blockable           = true;
                proj.m_dodgeable           = true;
            }

            // Add trailing frost to projectile
            GameObject arrowFrost = ZNetScene.instance?.GetPrefab("ArrowFrost");
            if (arrowFrost != null)
            {
                foreach (Transform child in arrowFrost.transform)
                {
                    if (child.GetComponentInChildren<ParticleSystem>(true) != null)
                    {
                        GameObject trail = Object.Instantiate(child.gameObject, projPrefab.transform);
                        trail.name = "vfx_projectile_trail";
                        trail.transform.localPosition = Vector3.zero;
                        trail.transform.localRotation = Quaternion.identity;

                        // Strip any arrow mesh — we only want the particle trail, not the visual geometry.
                        foreach (var mr in trail.GetComponentsInChildren<MeshRenderer>(true))
                            Object.Destroy(mr);
                        foreach (var mf in trail.GetComponentsInChildren<MeshFilter>(true))
                            Object.Destroy(mf);

                        foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = ps.main;
                            main.playOnAwake = true;
                            ps.Play();
                        }
                        break; // Just need the first VFX node we find on the arrow
                    }
                }
            }

            return projPrefab;
        }
    }
}
