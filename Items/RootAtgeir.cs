using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;
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
    // Cultivator third stance). The grafted mesh keeps its metal, shifted cool toward
    // iron (see IronMaterialTint); stats are our own.
    //
    // Every hit lands poison (in m_damages) and a snare (movement slow). The atgeir
    // gives Reap for free (native thrust primary + native 360° spin secondary). The
    // stance toggle cycles three farmer's verbs, each named for its motion:
    //   • Reap   (default): native atgeir 360° spin — the wide scything cut
    //             → snare (SE_VineSnare)
    //   • Harrow (stance B): the two-handed SLEDGE'S PRIMARY (overhead smash),
    //             borrowed as the secondary — breaking ground → root (SE_VineRoot)
    //   • Tend   (stance C): a real farming tool — the Cultivator's own PieceTable
    //             goes on the shared data, so vanilla place mode takes over
    //             (till/plant, build HUD, ghost). No attacks while tending;
    //             InPlaceMode() consumes the attack input by design.
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
        public enum Stance { Reap, Harrow, Tend }

        public Stance CurrentStance { get; private set; } = Stance.Reap;
        public bool IsHarrowStance => CurrentStance == Stance.Harrow;

        // The poison-green slam dust (a retinted vfx_sledge_iron_hit clone).
        // Registered in ZNetScene by ObjectDBPatch.
        public GameObject HarrowHitVfxPrefab { get; private set; }

        private ItemDrop.ItemData.SharedData _shared;
        private Attack _reapAttack;   // Reap: native atgeir spin (AoE)
        private Attack _smashAttack;  // Harrow: sledge PRIMARY overhead smash (AoE)
        private PieceTable _cultivatorPieces; // Tend: the Cultivator's own table

        // The slam's impact weight (dust burst, heavy hit sound, camera shake) lives on
        // the sledge's WEAPON-level effect lists — the cloned Attack only carries the
        // attack-level ones — so they're swapped in per-swing for the Harrow smash.
        // The Harrow lists are OUR clones (seeded from the sledge's): the slam is a
        // poison + root attack, so entries can be re-flavored/added here without
        // touching what real sledges spawn.
        private EffectList _atgeirHitEffect;
        private EffectList _atgeirTriggerEffect;
        private EffectList _harrowHitEffect;
        private EffectList _harrowTriggerEffect;

        // Player.SetPlaceMode is protected; it both stores the table and refreshes
        // the available-pieces list, so it's the one entry point worth calling.
        private static readonly System.Reflection.MethodInfo SetPlaceModeMethod =
            HarmonyLib.AccessTools.Method(typeof(Player), "SetPlaceMode");

        // Iron pivot (2026-07-07): the tool keeps its metal — vanilla cultivator
        // materials with the metallic response intact (iron backs it in the recipe
        // now), shifted cool so the golden-bronze fork head reads as iron and the
        // wood shaft weathers grey with it. Set as _Color; the shader multiplies it
        // over the albedo, so the vanilla brightness (see dev/screenshots/
        // cultivator_vanilla.png) survives where the old dark green tint went black.
        private static readonly Color IronMaterialTint = new Color(0.60f, 0.72f, 0.95f, 1f);
        private static readonly Color IronIconTint     = new Color(0.70f, 0.78f, 0.95f, 1f);

        // Poison-green multiplier for the Harrow slam's dust burst.
        private static readonly Color PoisonDustTint = new Color(0.45f, 0.85f, 0.30f, 1f);

        // Hand-painted albedo override (the Root-Strand Coil's twin — see RootSpear):
        // per-part color (dark wood handle / iron-grey fork head) painted per-pixel
        // using the cultivator_m metal mask, replacing the whole-material
        // IronMaterialTint when present (_Color goes white; the paint carries the
        // color). Loose PNG next to the plugin DLL wins, else embedded Assets copy.
        // Source dump: tsp_dumptex RootAtgeir (DEBUG builds); generator:
        // dev/paint_vinery_albedos.py.
        private const string AlbedoOverrideFile = "rootatgeir_albedo.png";

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
            // One material covers shaft + head, so there's no per-part green: the whole
            // tool goes iron-and-weathered-wood, and the vine identity lives in the
            // effects (green slam dust, vine rope, root holds). No matte pass — the
            // metallic response IS the look now.
            VisualUtil.TintMaterials(prefab, IronMaterialTint);
            VisualUtil.ZeroEmission(prefab);
            bool painted = ApplyAlbedoOverride(prefab);
            VisualUtil.DumpMaterials(prefab, "TheFurrowingShare");

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
            // Icon follows the held mesh: with the painted albedo active, snapshot the
            // real (painted) mesh — the attach node holds the grafted cultivator
            // visuals. Fallback chain: iron-tinted Cultivator icon, then the atgeir's
            // own icon (if the Cultivator can't be found).
            Sprite snapshot = painted
                ? IconSnapshot.Render(
                    prefab.transform.Find("attach")?.gameObject,
                    focus: 0.65f,  // fork prominent, hint of handle
                    spin: 25f,     // 3/4 view — show the fork's face, not its profile
                    label: "RootAtgeir")
                : null;
            if (snapshot != null)
            {
                shared.m_icons = new[] { snapshot };
            }
            else
            {
                Sprite[] cultIcons = ZNetScene.instance?.GetPrefab("Cultivator")
                    ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
                shared.m_icons = VisualUtil.TintIcons(
                    (cultIcons != null && cultIcons.Length > 0) ? cultIcons : shared.m_icons,
                    IronIconTint);
            }

            // Swamp-tier, sitting deliberately under Iron Atgeir (65 pierce) on raw
            // physical — the crowd-control utility is the payoff. Mirrors the Rockery
            // pattern: physical (pierce) is STATIC; only the elemental (our poison)
            // grows per level. Both weapon types are pierce-primary natively.
            shared.m_damages = new HitData.DamageTypes
            {
                m_pierce = 50f,
                m_poison = 15f,
            };
            shared.m_damagesPerLevel = new HitData.DamageTypes
            {
                m_poison = 10f,
            };
            shared.m_attackForce        = 60f;
            shared.m_maxDurability       = 200f;
            shared.m_durabilityPerLevel  = 50f;
            shared.m_weight              = 2.5f;
            shared.m_maxQuality          = 4;
            shared.m_maxStackSize        = 1;

            // Reap = the native atgeir spin (the clone's own secondary). Harrow = the
            // two-handed sledge's PRIMARY overhead smash, borrowed as the secondary.
            _reapAttack = shared.m_secondaryAttack;
            ItemDrop.ItemData.SharedData sledgeShared = ZNetScene.instance.GetPrefab("SledgeIron")
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            _smashAttack = sledgeShared?.m_attack;
            if (_smashAttack == null)
            {
                Log.Warn("RootAtgeir: SledgeIron primary not found — Harrow falls back to the reap spin");
                _smashAttack = _reapAttack;
            }
            else
            {
                // Clone so setting Harrow's stamina below doesn't mutate the shared
                // SledgeIron attack (which every iron sledge would otherwise inherit).
                _smashAttack = _smashAttack.Clone();

                // Seed the Harrow slam effects from the sledge's weapon-level lists
                // (cloned so the poison/root re-flavoring pass can edit them freely;
                // PrepareAttackEffect routes them per-swing).
                _harrowHitEffect     = VisualUtil.CloneEffectList(sledgeShared.m_hitEffect);
                _harrowTriggerEffect = VisualUtil.CloneEffectList(sledgeShared.m_triggerEffect);
            }
            _atgeirHitEffect     = shared.m_hitEffect;
            _atgeirTriggerEffect = shared.m_triggerEffect;
            BuildHarrowSlamVfx();
#if DEBUG
            VisualUtil.DumpEffectList("HarrowSlam.hit", _harrowHitEffect);
            VisualUtil.DumpEffectList("HarrowSlam.trigger", _harrowTriggerEffect);
            DumpBlobAttackEffects();
#endif

            // Lighter than iron — Iron Atgeir costs 14/28; the Share costs 12/24.
            // Both secondary stances (reap spin, harrow smash) share the 24 cost.
            if (shared.m_attack != null) shared.m_attack.m_attackStamina = 12f;
            _reapAttack.m_attackStamina = 24f;
            _smashAttack.m_attackStamina = 24f;

            // Default to Reap (the clone already carries the atgeir spin). Per-swing
            // routing sets the actual on-hit effect (see PrepareAttackEffect).
            shared.m_attackStatusEffect       = VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;

            // Tend stance: adopt the Cultivator's own piece table (till/plant).
            _cultivatorPieces = ZNetScene.instance?.GetPrefab("Cultivator")
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
            if (_cultivatorPieces == null)
                Log.Warn("RootAtgeir: Cultivator piece table not found — Tend stance disabled");

            return prefab;
        }

        // Applies the hand-painted albedo (see AlbedoOverrideFile) to the grafted
        // cultivator material: the painted texture replaces cultivator_bronze_d and
        // the iron tint goes white so the paint carries the color. Returns whether
        // the override actually landed (drives the snapshot-icon decision).
        private static bool ApplyAlbedoOverride(GameObject prefab)
        {
            Texture2D custom = VisualUtil.LoadOverrideTexture(AlbedoOverrideFile);
            if (custom == null) return false;
            int swapped = VisualUtil.SwapAlbedo(prefab, "Cultivator", custom, Color.white);
            if (swapped == 0)
            {
                Log.Warn("RootAtgeir.ApplyAlbedoOverride: no Cultivator material matched — override not applied");
                return false;
            }
            Log.Debug($"RootAtgeir.ApplyAlbedoOverride: {swapped} material(s) overridden");
            return true;
        }

        // The sledge dust (vfx_sledge_iron_hit, in the borrowed TRIGGER list) is the
        // slam's visual weight, but it reads plain grey under a poison weapon. Swap in
        // a poison-green retinted clone. The camshake and the heavy iron hit sound are
        // kept as-is (the Blob's attack sound was tried and reverted — too soft for a
        // two-handed slam). The entries array is ours (CloneEffectList); the replaced
        // ENTRY is built fresh so the vanilla EffectData isn't touched.
        private void BuildHarrowSlamVfx()
        {
            if (_harrowTriggerEffect?.m_effectPrefabs == null) return;

            GameObject src = ZNetScene.instance.GetPrefab("vfx_sledge_iron_hit");
            if (src == null)
            {
                Log.Warn("RootAtgeir.BuildHarrowSlamVfx: vfx_sledge_iron_hit not found — slam dust stays grey");
                return;
            }

            GameObject vfx = Object.Instantiate(src, Plugin.PrefabContainer);
            vfx.name = "TSP_vfx_harrow_hit";
            VisualUtil.TintParticles(vfx, PoisonDustTint);
            HarrowHitVfxPrefab = vfx;

            var entries = _harrowTriggerEffect.m_effectPrefabs;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i]?.m_prefab != src) continue;
                entries[i] = new EffectList.EffectData
                {
                    m_prefab  = vfx,
                    m_variant = -1,
                };
                Log.Debug("RootAtgeir.BuildHarrowSlamVfx: slam dust → TSP_vfx_harrow_hit (poison green)");
                return;
            }
            Log.Warn("RootAtgeir.BuildHarrowSlamVfx: sledge dust entry not found in the Harrow trigger list");
        }

#if DEBUG
        // DEBUG donor scouting: the Blob's poison-cloud puff is a candidate layer for
        // the Harrow slam's hit list (poison flavor). Log its attack items' effect
        // lists + projectiles so the cloud/puff prefab can be picked by name from an
        // in-game session, instead of guessing.
        private static void DumpBlobAttackEffects()
        {
            Humanoid blob = ZNetScene.instance?.GetPrefab("Blob")?.GetComponent<Humanoid>();
            if (blob == null || blob.m_defaultItems == null)
            {
                Log.Debug("[FxDump:Blob] Blob prefab or default items not found");
                return;
            }
            foreach (GameObject item in blob.m_defaultItems)
            {
                var itemShared = item?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (itemShared == null) continue;
                string projectile = itemShared.m_attack?.m_attackProjectile != null
                    ? itemShared.m_attack.m_attackProjectile.name
                    : "<none>";
                Log.Debug($"[FxDump:Blob] item '{item.name}' type={itemShared.m_attack?.m_attackType} projectile={projectile}");
                VisualUtil.DumpEffectList($"Blob.{item.name}.trigger", itemShared.m_triggerEffect);
                VisualUtil.DumpEffectList($"Blob.{item.name}.hit", itemShared.m_hitEffect);
                VisualUtil.DumpEffectList($"Blob.{item.name}.attack.trigger", itemShared.m_attack?.m_triggerEffect);
                VisualUtil.DumpEffectList($"Blob.{item.name}.attack.hit", itemShared.m_attack?.m_hitEffect);
            }
        }
#endif

        // Gives The Furrowing Share the Cultivator's look by adopting the Cultivator's OWN
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

        // The root is a PROC (Ashlands Nature weapon pattern — theirs: 20% chance,
        // 10s full immobilize; ours: softer/shorter hold at a higher, Vinery-scaled
        // chance): 25% base + 50% × Vinery skill factor → 25% at skill 0, 75% at 100.
        private const float RootChanceBase       = 0.25f;
        private const float RootChanceSkillBonus = 0.50f;

        // Routes the per-swing on-hit effect (called from HumanoidStartAttackPatch):
        // the primary always snares; the Harrow-stance secondary ROLLS for root, and
        // a failed roll falls back to the snare (the slam never whiffs its CC), which
        // is why the roll happens here instead of via m_attackStatusEffectChance —
        // vanilla's chance gives "effect or nothing". Rolled per-SWING on the local
        // attacker: one slam applies the same result to every target it hits (the
        // weapon-wide SE slot allows nothing finer without patching HitData).
        // Also routes the weapon-level impact effects: the Harrow smash swaps in the
        // slam lists (seeded from the sledge's), every other swing restores the atgeir's.
        public void PrepareAttackEffect(ItemDrop.ItemData.SharedData shared, bool secondaryAttack)
        {
            bool harrowSmash = secondaryAttack && IsHarrowStance;
            bool root = harrowSmash && VineStatusEffects.Root != null && RollRootChance();
            shared.m_attackStatusEffect       = root ? (StatusEffect)VineStatusEffects.Root : VineStatusEffects.Snare;
            shared.m_attackStatusEffectChance = 1f;

            if (_harrowHitEffect != null)
            {
                shared.m_hitEffect     = harrowSmash ? _harrowHitEffect     : _atgeirHitEffect;
                shared.m_triggerEffect = harrowSmash ? _harrowTriggerEffect : _atgeirTriggerEffect;
            }
        }

        private static bool RollRootChance()
        {
            Player player = Player.m_localPlayer;
            float factor  = player != null ? player.GetSkillFactor(VinerySkill.SkillType) : 0f;
            float chance  = RootChanceBase + RootChanceSkillBonus * factor;
            bool rooted   = Random.value < chance;
            Log.Debug($"RootAtgeir.RollRootChance: chance={chance:0.###} → {(rooted ? "ROOT" : "snare")}");
            return rooted;
        }

        public void ToggleStance()
        {
            CurrentStance = NextStance(CurrentStance);
            ApplyStance();
        }

        private Stance NextStance(Stance current)
        {
            switch (current)
            {
                case Stance.Reap:   return Stance.Harrow;
                case Stance.Harrow: return _cultivatorPieces != null ? Stance.Tend : Stance.Reap;
                default:            return Stance.Reap;
            }
        }

        public void ApplyStance()
        {
            string msgKey;
            switch (CurrentStance)
            {
                case Stance.Harrow: msgKey = "$rootatgeir_stance_harrow"; break;
                case Stance.Tend:   msgKey = "$rootatgeir_stance_tend";   break;
                default:            msgKey = "$rootatgeir_stance_reap";   break;
            }
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msgKey);

            Player player = Player.m_localPlayer;
            if (player == null) return;

            // Swap the secondary attack: Reap = atgeir spin, Harrow = sledge overhead
            // smash. No m_animationState swap. Snare is the resting on-hit effect;
            // per-swing routing overrides it for the Harrow secondary (→ root).
            var equipped = player.GetCurrentWeapon()?.m_shared;
            if (equipped != null)
            {
                equipped.m_secondaryAttack = IsHarrowStance
                    ? (_smashAttack ?? equipped.m_secondaryAttack)
                    : (_reapAttack ?? equipped.m_secondaryAttack);
                equipped.m_attackStatusEffect = VineStatusEffects.Snare;

                // Tend: hand the shared data the Cultivator's piece table and
                // sync place mode now (SetupEquipment only re-reads m_buildPieces on
                // equip changes). Leaving the stance nulls both; UpdatePlacement
                // hides the placement ghost itself once InPlaceMode() goes false.
                equipped.m_buildPieces = CurrentStance == Stance.Tend ? _cultivatorPieces : null;
                SetPlaceModeMethod?.Invoke(player, new object[] { equipped.m_buildPieces });
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
