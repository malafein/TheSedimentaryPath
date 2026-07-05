using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.StatusEffects;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Root Atgeir — the Vinery "field" weapon: a two-handed vine polearm that works
    // a crowd. Split skill: Polearms × Vinery (see Plugin.SplitSkillWeapons),
    // mirroring the rockery weapons.
    //
    // Cloned from AtgeirBlackmetal for the weapon plumbing — the native atgeir
    // animations (thrust-combo primary, 360° spin secondary), 2H grip and attack data
    // all come along correctly. The HELD MESH is then swapped to the Cultivator's, so
    // it reads as a farming implement rather than any atgeir (and sets up the planned
    // Cultivator third stance). Tinted green over the grafted mesh; stats are our own.
    //
    // Every hit lands poison (in m_damages) and a snare (movement slow). The atgeir
    // gives Sweep for free (native thrust primary + native 360° spin secondary). The
    // stance toggle only swaps the SECONDARY attack:
    //   • Sweep  (default): native atgeir 360° spin       → snare (SE_VineSnare)
    //   • Furrow (stance B): the two-handed SLEDGE'S PRIMARY (overhead smash), borrowed
    //                        as the secondary               → root (SE_VineRoot)
    //
    // We borrow the sledge's PRIMARY (m_attack) — that's where sledges keep their big
    // overhead swing (their secondary slot isn't it). No m_animationState swap: like
    // Kaldmörk's knife throwing with spear_throw, the borrowed melee should resolve by
    // its m_attackAnimation trigger in the Player animator regardless of hold state.
    //
    // The primary always snares. m_attackStatusEffect is weapon-wide, so the per-swing
    // effect is routed by HumanoidStartAttackPatch → PrepareAttackEffect.
    public class RootAtgeir : IStanceWeapon
    {
        public bool IsSweepStance  { get; private set; } = true;
        public bool IsFurrowStance => !IsSweepStance;

        private ItemDrop.ItemData.SharedData _shared;
        private Attack _sweepAttack;  // Sweep: native atgeir spin (AoE)
        private Attack _smashAttack;  // Furrow: sledge PRIMARY overhead smash (AoE)

        // Green vine tint (set, not multiplied — keeps texture detail).
        private static readonly Color VineMaterialTint = new Color(0.18f, 0.42f, 0.12f, 1f);
        private static readonly Color VineIconTint     = new Color(0.30f, 0.55f, 0.22f, 1f);

        public GameObject CreatePrefab()
        {
            GameObject atgeirBase = ZNetScene.instance.GetPrefab("AtgeirBlackmetal");
            if (atgeirBase == null)
            {
                Log.Error("RootAtgeir.CreatePrefab: AtgeirBlackmetal not found");
                return null;
            }

            GameObject prefab = Object.Instantiate(atgeirBase, Plugin.PrefabContainer);
            prefab.name = TSPVineryWeapons.RootAtgeirPrefab;

            // Swap the held atgeir geometry for the Cultivator's mesh BEFORE tinting,
            // so TintMaterials clones + greens the grafted cultivator materials (the
            // real Cultivator's shared materials stay untouched).
            ApplyCultivatorMesh(prefab);
            VisualUtil.TintMaterials(prefab, VineMaterialTint);
            VisualUtil.ZeroEmission(prefab);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                Log.Error("RootAtgeir.CreatePrefab: no ItemDrop on clone");
                return null;
            }

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            _shared = shared;

            shared.m_name        = TSPVineryWeapons.RootAtgeirName;
            shared.m_description = "$item_rootatgeir_desc";
            // Native weapon skill; Vinery is the split partner (Plugin.SplitSkillWeapons).
            shared.m_skillType   = ValheimSkills.SkillType.Polearms;
            // Icon follows the held mesh: the Cultivator's icon, tinted green (falls
            // back to the atgeir's own icon if the Cultivator can't be found).
            Sprite[] cultIcons = ZNetScene.instance?.GetPrefab("Cultivator")
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
            shared.m_icons = VisualUtil.TintIcons(
                (cultIcons != null && cultIcons.Length > 0) ? cultIcons : shared.m_icons,
                VineIconTint);

            // Swamp-tier: slash + pierce carried over from the atgeir, poison added
            // and the only per-level growth (mirrors the Vinery poison identity).
            shared.m_damages = new HitData.DamageTypes
            {
                m_slash  = 45f,
                m_pierce = 20f,
                m_poison = 20f,
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes
            {
                m_poison = 8f,
            };
            shared.m_attackForce        = 60f;
            shared.m_maxDurability       = 200f;
            shared.m_durabilityPerLevel  = 50f;
            shared.m_weight              = 2.5f;
            shared.m_maxQuality          = 4;
            shared.m_maxStackSize        = 1;

            // Sweep = the native atgeir spin (the clone's own secondary). Furrow = the
            // two-handed sledge's PRIMARY overhead smash, borrowed as the secondary.
            _sweepAttack = shared.m_secondaryAttack;
            _smashAttack = ZNetScene.instance.GetPrefab("SledgeIron")
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack;
            if (_smashAttack == null)
            {
                Log.Warn("RootAtgeir: SledgeIron primary not found — Furrow falls back to the sweep spin");
                _smashAttack = _sweepAttack;
            }

            // Default to Sweep (the clone already carries the atgeir spin). Per-swing
            // routing sets the actual on-hit effect (see PrepareAttackEffect).
            shared.m_attackStatusEffect       = VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;

            return prefab;
        }

        // Gives The Furrowing the Cultivator's look by adopting the Cultivator's OWN
        // "attach" node — both its grip transform and its visual children. The Cultivator
        // already knows how to sit in a hand, so this places the mesh with its authored
        // orientation instead of forcing its geometry into the atgeir's mesh pivot (which
        // swung it out of the grip). The atgeir's own held geometry is hidden. Falls back
        // to the (tinted) atgeir mesh if the Cultivator can't be found.
        private void ApplyCultivatorMesh(GameObject weaponPrefab)
        {
            Transform atgeirAttach = weaponPrefab.transform.Find("attach");
            if (atgeirAttach == null)
            {
                Log.Warn("RootAtgeir.ApplyCultivatorMesh: no 'attach' node — keeping atgeir mesh");
                return;
            }

            GameObject cultivator = ZNetScene.instance?.GetPrefab("Cultivator");
            Transform cultAttach = cultivator != null ? cultivator.transform.Find("attach") : null;
            if (cultAttach == null)
            {
                Log.Warn("RootAtgeir.ApplyCultivatorMesh: Cultivator/attach not found — keeping atgeir mesh (tinted)");
                return;
            }

            // Hide the atgeir's own held geometry.
            foreach (Renderer r in atgeirAttach.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            // Adopt the Cultivator's grip transform (how the Cultivator itself is held).
            atgeirAttach.localPosition = cultAttach.localPosition;
            atgeirAttach.localRotation = cultAttach.localRotation;
            atgeirAttach.localScale    = cultAttach.localScale;

            // Copy in the Cultivator's visual children, preserving their local transforms.
            int copied = 0;
            foreach (Transform child in cultAttach)
            {
                GameObject copy = Object.Instantiate(child.gameObject, atgeirAttach);
                copy.name = "cultivator_" + child.name;
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale    = child.localScale;
                copied++;
            }

            if (copied == 0)
                Log.Warn("RootAtgeir.ApplyCultivatorMesh: Cultivator attach had no visual children — keeping atgeir mesh (tinted)");
            else
                Log.Debug($"RootAtgeir.ApplyCultivatorMesh: adopted Cultivator attach ({copied} visual child(ren))");
        }

        // Routes the per-swing on-hit effect (called from HumanoidStartAttackPatch):
        // the primary always snares; only the Furrow-stance secondary roots.
        public void PrepareAttackEffect(ItemDrop.ItemData.SharedData shared, bool secondaryAttack)
        {
            bool root = secondaryAttack && IsFurrowStance && VineStatusEffects.Root != null;
            shared.m_attackStatusEffect       = root ? (StatusEffect)VineStatusEffects.Root : VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;
        }

        public void ToggleStance()
        {
            IsSweepStance = !IsSweepStance;
            ApplyStance();
        }

        public void ApplyStance()
        {
            string msgKey = IsSweepStance ? "$rootatgeir_stance_sweep" : "$rootatgeir_stance_furrow";
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msgKey);

            Player player = Player.m_localPlayer;
            if (player == null) return;

            // Swap the secondary attack: Sweep = atgeir spin, Furrow = sledge overhead
            // smash. No m_animationState swap. Snare is the resting on-hit effect;
            // per-swing routing overrides it for the Furrow secondary (→ root).
            var equipped = player.GetCurrentWeapon()?.m_shared;
            if (equipped != null)
            {
                equipped.m_secondaryAttack = IsSweepStance
                    ? (_sweepAttack ?? equipped.m_secondaryAttack)
                    : (_smashAttack ?? equipped.m_secondaryAttack);
                equipped.m_attackStatusEffect = VineStatusEffects.Snare;
            }

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
