using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    public class ObsidianSword : IStanceWeapon
    {
        // Stance A = SwordSilver secondary (preserved vanilla).
        // Stance B = TBD — mirrors A until the alternate secondary is decided.
        public bool IsThrustStance { get; private set; } = true;

        private ItemDrop.ItemData.SharedData _shared;
        private Attack _thrustAttack;
        private Attack _leapAttack;

        public GameObject CreatePrefab()
        {
            if (Plugin.KaldmorkPrefab == null)
            {
                Log.Error("ObsidianSword.CreatePrefab: KaldmorkPrefab not yet registered");
                return null;
            }

            // Clone the already-tinted dagger prefab — consistent obsidian coloring with no extra work.
            GameObject prefab = Object.Instantiate(Plugin.KaldmorkPrefab, Plugin.PrefabContainer);
            prefab.name = "Dokkblad";

            // Stretch the blade mesh along its local Y axis to sword length.
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null)
                mf.transform.localScale = new Vector3(1f, 2.5f, 1f);
            else
                Log.Warn("ObsidianSword: no MeshFilter found — blade stretch skipped");

            // The ambient VFX cloned from the dagger sits at z=0.25 — move it further along the longer blade.
            Transform ambientVfx = prefab.transform.Find("attach/vfx_frost_ambient");
            if (ambientVfx != null)
                ambientVfx.localPosition = new Vector3(0f, 0f, 0.6f);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                Log.Error("ObsidianSword.CreatePrefab: no ItemDrop on clone");
                return null;
            }

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            _shared = shared;

            shared.m_name        = "$item_dokkblad";
            shared.m_description = "$item_dokkblad_desc";
            shared.m_skillType   = ValheimSkills.SkillType.Swords;
            // Icons are already tinted obsidian from the dagger clone — no re-tinting needed.

            // The dagger clone's m_secondaryAttack may be set to the throw attack (dagger
            // defaults to throw stance), so fetch the leap attack from KnifeChitin directly.
            GameObject knifeChitinPrefab = ZNetScene.instance.GetPrefab("KnifeChitin");
            _leapAttack = knifeChitinPrefab?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_secondaryAttack
                ?? shared.m_secondaryAttack;

            // Copy SwordSilver's primary attack and stance data so the character uses
            // sword animations (range, height, angle, hitbox, hold animation).
            GameObject swordSilverPrefab = ZNetScene.instance.GetPrefab("SwordSilver");
            if (swordSilverPrefab != null)
            {
                var swordShared = swordSilverPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (swordShared != null)
                {
                    shared.m_attack         = swordShared.m_attack;
                    shared.m_animationState = swordShared.m_animationState;
                    _thrustAttack = swordShared.m_secondaryAttack;
                }
                else
                {
                    Log.Warn("ObsidianSword: could not read SwordSilver SharedData — using knife attacks");
                    _thrustAttack = _leapAttack;
                }
            }
            else
            {
                Log.Warn("ObsidianSword: SwordSilver not found — using knife attacks");
                _thrustAttack = _leapAttack;
            }
            shared.m_secondaryAttack = IsThrustStance ? _thrustAttack : _leapAttack;

            // Slash is static; only frost grows per level (mirrors Kaldmörk's pattern).
            // At max quality (Q3): Slash 70, Frost 38.
            shared.m_damages = new HitData.DamageTypes
            {
                m_slash = 70f,
                m_frost = 18f,
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes
            {
                m_frost = 10f,
            };
            shared.m_attackForce        = 40f;
            shared.m_backstabBonus      = 3f;
            shared.m_maxDurability      = 175f;
            shared.m_durabilityPerLevel = 40f;
            shared.m_weight             = 1.5f;
            shared.m_blockPower         = 10f;
            shared.m_timedBlockBonus    = 2f;
            shared.m_deflectionForce    = 20f;
            shared.m_movementModifier   = -0.05f;
            shared.m_maxQuality         = 3;
            shared.m_maxStackSize       = 1;

            return prefab;
        }

        public void ToggleStance()
        {
            IsThrustStance = !IsThrustStance;
            ApplyStance();
        }

        public void ApplyStance()
        {
            string msgKey = IsThrustStance ? "$dokkblad_stance_a" : "$dokkblad_stance_b";
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msgKey);

            Player player = Player.m_localPlayer;
            if (player == null) return;

            var equippedShared = player.GetCurrentWeapon()?.m_shared;
            if (equippedShared != null)
                equippedShared.m_secondaryAttack = IsThrustStance ? _thrustAttack : _leapAttack;

            int seHash = "SE_WeaponStance".GetStableHashCode();
            var seman  = player.GetSEMan();
            var se = (seman.GetStatusEffect(seHash) ?? seman.AddStatusEffect(seHash)) as SE_WeaponStance;
            if (se != null)
            {
                string stanceName = Localization.instance.Localize(msgKey);
                Sprite icon = _shared?.m_icons?.Length > 0 ? _shared.m_icons[0] : null;
                se.Refresh(stanceName, icon);
            }
        }
    }
}
