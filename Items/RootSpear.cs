using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Root Spear — the Vinery "line" weapon: a one-handed vine spear that answers a
    // single enemy at reach. Split skill: Spears × Vinery (see Plugin.SplitSkillWeapons),
    // mirroring the rockery weapons. Cloned from SpearElderbark (wooden shaft reads as
    // vine when tinted green), so the native spear thrust and animations render correctly.
    //
    // Every hit lands poison (in m_damages) and a snare (movement slow). Both stances
    // share ONE secondary attack — a throw that CONSUMES the spear (m_consumeItem): it
    // leaves your hand and becomes the thrown projectile, "on a vine" (LineConnect rope
    // to you). What differs is the direction of the grapple:
    //   • Cast  (default): reels the ENEMY to you (SE_VineTether, target-side,
    //                      server-authoritative for creatures)
    //   • Vault (stance B): pulls YOU to the enemy (RootSpearProjectile, thrower-side)
    // The spear returns to your hand at the end-point — when you meet the reeled
    // creature, when you walk over to a landed (missed) spear, or when the reel window
    // elapses (the vine yanks it home). RootSpearProjectile owns the return; see there.
    //
    // The primary always snares. m_attackStatusEffect is weapon-wide, so the per-swing
    // effect is routed by HumanoidStartAttackPatch → PrepareAttackEffect. The thrown
    // projectile is cloned from the Abyssal Harpoon's projectile, which carries a
    // LineConnect (rope projectile → owner) for the vine tether visual, for free — and
    // it's a networked ZNetView object, so the rope + stuck spear are visible to all.
    public class RootSpear : IStanceWeapon
    {
        public bool IsCastStance  { get; private set; } = true;
        public bool IsVaultStance => !IsCastStance;

        // Registered in ZNetScene by ObjectDBPatch (has a ZNetView).
        public GameObject ProjectilePrefab { get; private set; }

        private ItemDrop.ItemData.SharedData _shared;

        private static readonly Color VineMaterialTint = new Color(0.18f, 0.42f, 0.12f, 1f);
        private static readonly Color VineIconTint     = new Color(0.30f, 0.55f, 0.22f, 1f);

        public GameObject CreatePrefab()
        {
            GameObject spearBase = ZNetScene.instance.GetPrefab("SpearElderbark");
            if (spearBase == null)
            {
                Log.Error("RootSpear.CreatePrefab: SpearElderbark not found");
                return null;
            }

            GameObject prefab = Object.Instantiate(spearBase, Plugin.PrefabContainer);
            prefab.name = TSPVineryWeapons.RootSpearPrefab;

            VisualUtil.TintMaterials(prefab, VineMaterialTint);
            VisualUtil.ZeroEmission(prefab);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                Log.Error("RootSpear.CreatePrefab: no ItemDrop on clone");
                return null;
            }

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            _shared = shared;

            shared.m_name        = TSPVineryWeapons.RootSpearName;
            shared.m_description = "$item_rootspear_desc";
            // Native weapon skill; Vinery is the split partner (Plugin.SplitSkillWeapons).
            shared.m_skillType   = ValheimSkills.SkillType.Spears;
            shared.m_icons       = VisualUtil.TintIcons(shared.m_icons, VineIconTint);

            shared.m_damages = new HitData.DamageTypes
            {
                m_pierce = 40f,
                m_poison = 18f,
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes
            {
                m_poison = 7f,
            };
            shared.m_attackForce        = 30f;
            shared.m_maxDurability       = 150f;
            shared.m_durabilityPerLevel  = 30f;
            shared.m_weight              = 1.5f;
            shared.m_maxQuality          = 4;
            shared.m_maxStackSize        = 1;

            // The returning vine-throw projectile (shared by both stances).
            ProjectilePrefab = CreateProjectilePrefab(spearBase);

            // Secondary = a returning throw. Both stances use it; Cast vs Vault only
            // changes the routed on-hit effect (and the client-side self-pull).
            shared.m_secondaryAttack = BuildThrowAttack(ProjectilePrefab);

            // Default to Cast. Per-swing routing sets the actual on-hit effect.
            shared.m_attackStatusEffect       = VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;

            return prefab;
        }

        // A spear throw (spear_throw animation, matched close to a vanilla spear's feel).
        // Consumes the spear on throw — it leaves the hand and becomes the projectile;
        // RootSpearProjectile returns it at the end-point. (Cloning the base spear's own
        // secondary was tried and reverted: that slot isn't the throw, so the clone was a
        // non-throwing attack — no projectile, no rope, no hit.)
        private static Attack BuildThrowAttack(GameObject projectile)
        {
            return new Attack
            {
                m_attackType         = Attack.AttackType.Projectile,
                m_attackAnimation    = "spear_throw",
                m_attackProjectile   = projectile,   // our vine projectile (rope + reel)
                m_projectileVel      = 30f,
                m_projectileVelMin   = 18f,
                m_projectileAccuracy = 0.5f,
                m_consumeItem        = true,          // leaves the hand; returned at the end-point
                m_attackHeight       = 1.3f,
                m_attackRange        = 1.0f,
                m_launchAngle        = 0f,
                m_attackStamina      = 15f,
            };
        }

        // Cloned from the Abyssal Harpoon's projectile so it brings a LineConnect
        // (rope → owner) and sticks into the target. Retinted, carries the Vault
        // self-pull component, and never drops a spear copy on hit.
        private GameObject CreateProjectilePrefab(GameObject spearBase)
        {
            // Use the spear's OWN thrown projectile — you throw your spear, not a harpoon.
            // The throw lives on the spear's secondary; fall back to its primary, then (last
            // resort) the harpoon. This also sheds the harpoon-specific baggage.
            var spearSh = spearBase.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            GameObject baseProj = spearSh?.m_secondaryAttack?.m_attackProjectile
                               ?? spearSh?.m_attack?.m_attackProjectile;
            if (baseProj == null)
            {
                baseProj = ZNetScene.instance.GetPrefab("SpearChitin")
                    ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack?.m_attackProjectile;
                Log.Warn("RootSpear: elderbark spear projectile not found — falling back to the harpoon projectile");
            }
            if (baseProj == null)
            {
                Log.Error("RootSpear.CreateProjectilePrefab: no base projectile found — throw disabled");
                return null;
            }

            GameObject projPrefab = Object.Instantiate(baseProj, Plugin.PrefabContainer);
            projPrefab.name = "RootSpear_projectile";
            VisualUtil.TintMaterials(projPrefab, VineMaterialTint);
            VisualUtil.ZeroEmission(projPrefab);

            Projectile proj = projPrefab.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.m_statusEffect        = "";    // don't force a default SE; HitData routes ours
                proj.m_respawnItemOnHit    = false; // no dropped pickup — RootSpearProjectile vine-returns it
                proj.m_spawnOnTtl          = false;
                proj.m_stayAfterHitDynamic = true;  // stick in the creature
                proj.m_stayAfterHitStatic  = true;  // stick in the ground on a miss (the landed spear)
                proj.m_attachToRigidBody   = true;  // embed INTO the creature (spears normally just drop)
                proj.m_attachToClosestBone = true;
                proj.m_stayTTL             = 4f;    // stays through the reel/retrieve window
                proj.m_stopEmittersOnHit   = true;  // our LineConnect rope is the persistent vine now
            }

            // The chitin-harpoon projectile has no rope (its in-flight vine is a trail that
            // dies on impact). Add a persistent one so the vine connects you to the spear
            // through flight AND while stuck, until retrieval.
            AddVineRope(projPrefab);

            projPrefab.AddComponent<RootSpearProjectile>();
            return projPrefab;
        }

        // Adds a persistent rope (LineConnect + LineRenderer) from the spear to the thrower.
        // Reuses the vanilla harpoon rope's material (the rope texture) tinted green. The
        // LineConnect sits on the projectile root, so Projectile.Setup wires its peer to the
        // owner automatically, and it renders on every client (networked ZDO).
        private void AddVineRope(GameObject projPrefab)
        {
            if (projPrefab.GetComponent<LineConnect>() != null) return;

            LineRenderer srcLR = null;
            StatusEffect harpoonSE = ZNetScene.instance.GetPrefab("SpearChitin")
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attackStatusEffect;
            if (harpoonSE?.m_startEffects?.m_effectPrefabs != null)
            {
                foreach (var ed in harpoonSE.m_startEffects.m_effectPrefabs)
                {
                    LineRenderer slr = ed?.m_prefab != null ? ed.m_prefab.GetComponentInChildren<LineRenderer>(true) : null;
                    if (slr != null) { srcLR = slr; break; }
                }
            }

            LineRenderer lr = projPrefab.GetComponent<LineRenderer>() ?? projPrefab.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);
            if (srcLR != null)
            {
                lr.widthMultiplier   = srcLR.widthMultiplier;
                lr.textureMode       = srcLR.textureMode;
                lr.numCapVertices    = srcLR.numCapVertices;
                lr.numCornerVertices = srcLR.numCornerVertices;
                lr.alignment         = srcLR.alignment;
                lr.shadowCastingMode = srcLR.shadowCastingMode;
                if (srcLR.sharedMaterial != null)
                {
                    Material m = new Material(srcLR.sharedMaterial);
                    if (m.HasProperty("_Color")) m.color = VineMaterialTint;
                    lr.sharedMaterial = m;
                }
            }
            else
            {
                lr.widthMultiplier = 0.06f;
                Log.Warn("RootSpear.AddVineRope: harpoon rope material not found — using a plain line width");
            }
            lr.startColor = lr.endColor = Color.white;

            LineConnect lc = projPrefab.AddComponent<LineConnect>();
            lc.m_hideIfNoConnection = true;
            lc.m_centerOfCharacter  = true;  // connect to the thrower's center, not their feet
            lc.m_dynamicThickness   = false;
            lc.m_dynamicSlack       = true;
            lc.m_slack              = 0.3f;

            Log.Debug($"RootSpear.AddVineRope: rope added (harpoon material={(srcLR?.sharedMaterial != null)})");
        }

        // Routes the per-swing on-hit effect (called from HumanoidStartAttackPatch):
        // the primary always snares; only the Cast-stance secondary tethers (reels the
        // target). Vault's secondary snares here and self-pulls via the projectile.
        public void PrepareAttackEffect(ItemDrop.ItemData.SharedData shared, bool secondaryAttack)
        {
            bool tether = secondaryAttack && IsCastStance && VineStatusEffects.Tether != null;
            shared.m_attackStatusEffect       = tether ? (StatusEffect)VineStatusEffects.Tether : VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;
        }

        public void ToggleStance()
        {
            IsCastStance = !IsCastStance;
            ApplyStance();
        }

        public void ApplyStance()
        {
            string msgKey = IsCastStance ? "$rootspear_stance_cast" : "$rootspear_stance_vault";
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msgKey);

            Player player = Player.m_localPlayer;
            if (player == null) return;

            var equipped = player.GetCurrentWeapon()?.m_shared;
            if (equipped != null)
                equipped.m_attackStatusEffect = VineStatusEffects.Snare;

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
