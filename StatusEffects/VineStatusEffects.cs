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
            }

            if (Root == null)
            {
                Root = ScriptableObject.CreateInstance<SE_Stats>();
                Root.name            = RootEffectName;
                Root.m_name          = "$se_vineroot";
                Root.m_ttl           = 6f;
                Root.m_speedModifier = -0.9f; // near-immobilized: roots the target in place
                Root.m_icon          = rootIcon;
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

        private static Sprite FirstIcon(ObjectDB db, string prefabName)
        {
            Sprite[] icons = db?.GetItemPrefab(prefabName)
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
            return (icons != null && icons.Length > 0) ? icons[0] : null;
        }
    }
}
