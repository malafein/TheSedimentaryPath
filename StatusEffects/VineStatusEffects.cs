using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // Shared home for the vinery weapons' on-hit status effects. Built once from
    // ObjectDBPatch and registered into ObjectDB.m_StatusEffects so they resolve by
    // name-hash when a HitData applies them. Both RootAtgeir and RootSpear read
    // these instances when routing their per-swing m_attackStatusEffect.
    //
    //   Snare  — the baseline vine slow, on every hit of both weapons.
    //   Root   — a near-immobilizing hold for the atgeir's Furrow slam.
    //   Tether — the spear's Cast reel (pulls the target to the attacker);
    //            also lays the snare on its victim (see SE_VineTether).
    public static class VineStatusEffects
    {
        public const string SnareEffectName  = "SE_VineSnare";
        public const string RootEffectName   = "SE_VineRoot";
        public const string TetherEffectName = "SE_VineTether";

        public static readonly int SnareHash = SnareEffectName.GetStableHashCode();

        // Snare cloud color: multiplies the vanilla poisoned-cloud's own green,
        // so values < 1 darken it. Kept clearly darker than a live Poison so the
        // two read apart when the same hit applies both. In-game tuning knob.
        private static readonly Color SnareCloudTint = new Color(0.35f, 0.5f, 0.35f, 1f);

        public static SE_Stats Snare  { get; private set; }
        public static SE_Stats Root   { get; private set; }
        public static SE_VineTether Tether { get; private set; }

        // Idempotent: safe to call on every ObjectDB.Awake. Icons borrow the Root
        // item art so the effects surface in the target's HUD without custom assets.
        public static void Build(ObjectDB db)
        {
            Sprite rootIcon = FirstIcon(db, "Root");

            if (Snare == null)
            {
                Snare = ScriptableObject.CreateInstance<SE_Stats>();
                Snare.name            = SnareEffectName;
                Snare.m_name          = "$se_vinesnare";
                Snare.m_ttl           = 4f;
                Snare.m_speedModifier = -0.45f; // ~45% movement slow
                Snare.m_icon          = rootIcon;

                // Subtle held-cue (the full root-grab vfx would be spam — the
                // snare lands on EVERY hit of both weapons): the vanilla
                // poisoned-cloud, retinted darker so it reads apart from the
                // real poisoning the same hit usually deals. Donor is the
                // Poison SE itself — no prefab-name guessing. Cosmetic only:
                // a miss leaves the snare functional but invisible.
                StatusEffect poisonDonor = db?.m_StatusEffects?.Find(
                    se => se != null && se.name == "Poison");
                if (poisonDonor != null && poisonDonor.m_startEffects.HasEffects())
                {
                    Snare.m_startEffects = VisualUtil.CloneEffectListRetinted(
                        poisonDonor.m_startEffects,
                        SnareCloudTint,
                        "TSP_vfx_vinesnare");
                    VisualUtil.DumpEffectList("SE_VineSnare.m_startEffects", Snare.m_startEffects);
                }
                else
                {
                    Log.Warn("VineStatusEffects: Poison SE donor has no start effects — snare stays without a visual");
                }
            }

            if (Root == null)
            {
                Root = ScriptableObject.CreateInstance<SE_Stats>();
                Root.name            = RootEffectName;
                Root.m_name          = "$se_vineroot";
                Root.m_ttl           = 6f;
                Root.m_speedModifier = -0.9f; // near-immobilized: roots the target in place
                Root.m_icon          = rootIcon;

                // The Ashlands "Nature" weapons chance-apply a rooted hold whose SE
                // carries the authored root-grab visuals on the target. Adopt those
                // start effects (as our own clone, editable) so the Harrow slam's
                // hold READS as roots, not just a movement debuff.
                StatusEffect rootedDonor = FindRootedDonor(db);
                if (rootedDonor != null)
                    Root.m_startEffects = VisualUtil.CloneEffectList(rootedDonor.m_startEffects);
            }

            if (Tether == null)
            {
                Tether = ScriptableObject.CreateInstance<SE_VineTether>();
                Tether.name   = TetherEffectName;
                Tether.m_name = "$se_vinesnare"; // reads as "Snared" in the HUD
                Tether.m_ttl  = 1.4f;            // reel window (Impulse is mass-scaled → needs time)
                Tether.m_icon = rootIcon;
            }
        }

        // Finds the vanilla rooted status effect via the Ashlands "Nature" weapons that
        // chance-apply it, walking a donor list in case any given item is renamed. The
        // staff is last: its root effect may ride a summon rather than the item itself.
        private static StatusEffect FindRootedDonor(ObjectDB db)
        {
            string[] donors =
            {
                "THSwordSlayerNature",
                "MaceEldnerNature",
                "CrossbowRipperNature",
                "StaffGreenRoots",
            };
            foreach (string name in donors)
            {
                StatusEffect se = db?.GetItemPrefab(name)
                    ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attackStatusEffect;
                if (se?.m_startEffects?.m_effectPrefabs != null &&
                    se.m_startEffects.m_effectPrefabs.Length > 0)
                {
                    Log.Debug($"VineStatusEffects: rooted-visual donor '{name}' → SE '{se.name}' ({se.GetType().Name})");
                    VisualUtil.DumpEffectList($"{se.name}.m_startEffects", se.m_startEffects);
                    return se;
                }
            }
            Log.Warn("VineStatusEffects: no Nature rooted-effect donor found — vine root has no visual");
            return null;
        }

        private static Sprite FirstIcon(ObjectDB db, string prefabName)
        {
            Sprite[] icons = db?.GetItemPrefab(prefabName)
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
            return (icons != null && icons.Length > 0) ? icons[0] : null;
        }
    }
}
