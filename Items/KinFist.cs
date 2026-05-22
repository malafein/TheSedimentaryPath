using UnityEngine;
using ValheimSkills = global::Skills;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // KinFist — the awakened-by-the-stone fist.
    //
    // NOT an inventory item. KinFist is an `ItemDrop.ItemData` instance
    // that lives only in memory and is substituted in for the player's
    // `m_unarmedWeapon` when:
    //   • the local player has SE_StoneKin active at tier 3, AND
    //   • the StoneKinPredicate holds (unarmored + kin-handed), AND
    //   • the right hand is empty (so m_unarmedWeapon is the active weapon)
    //
    // The substitution is performed by HumanoidGetCurrentWeaponPatch on
    // every Humanoid.GetCurrentWeapon call. The player's actual
    // `m_unarmedWeapon` is never mutated — no save/load, respawn, or
    // ownership concerns. When any condition flips false, vanilla fists
    // come back automatically.
    //
    // Damage scales with shrine score at ritual time via the
    // 150 × score / (score + 82.5) curve; SetBluntDamage updates the
    // shared instance's m_blunt field. Block / parry / deflection stats
    // are cloned from FistFenrirClaw (Flesh Rippers) so the fist plays
    // like a fenrir-clawed berserker at the top tier.
    //
    // Skill = Unarmed (so IsBareFistHit elsewhere still recognizes the
    // punch as bare-fist for kin-rules). Registered in
    // Plugin.SplitSkillWeapons so damage scaling and XP split 50/50
    // between Unarmed and Rockery.
    public static class KinFist
    {
        // Localization key for the substituted weapon's display name.
        // Players will see this in damage popups and the hit feed.
        public const string ItemName = "$item_kinfist";

        public static ItemDrop.ItemData ItemData { get; private set; }

        // True once Build() has succeeded. HumanoidGetCurrentWeaponPatch
        // checks this — if false, tier-3 substitution silently no-ops
        // and the player gets vanilla fists.
        public static bool IsReady => ItemData != null;

        // Called from ObjectDBPatch.Postfix once ObjectDB is ready.
        // Clones FistFenrirClaw's shared data for block/parry/deflection;
        // overrides damage (set later per-ritual), durability, and skill.
        public static void Build()
        {
            if (ItemData != null) return;  // idempotent

            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                Log.Warn("KinFist.Build: ObjectDB.instance is null");
                return;
            }

            GameObject fenrirPrefab = db.GetItemPrefab("FistFenrirClaw");
            if (fenrirPrefab == null)
            {
                Log.Warn("KinFist.Build: FistFenrirClaw not in ObjectDB; tier-3 KinFist disabled");
                return;
            }

            ItemDrop fenrirDrop = fenrirPrefab.GetComponent<ItemDrop>();
            if (fenrirDrop == null || fenrirDrop.m_itemData?.m_shared == null)
            {
                Log.Warn("KinFist.Build: FistFenrirClaw has no usable ItemData");
                return;
            }

            ItemDrop.ItemData.SharedData fenrirShared = fenrirDrop.m_itemData.m_shared;

            // Build a fresh shared instance — we want a separate identity
            // so future Flesh Ripper buffs/nerfs don't drift KinFist.
            ItemDrop.ItemData.SharedData kinShared = new ItemDrop.ItemData.SharedData
            {
                m_name             = ItemName,
                m_description      = "Hands awakened by the stone.",
                m_itemType         = ItemDrop.ItemData.ItemType.OneHandedWeapon,
                m_skillType        = ValheimSkills.SkillType.Unarmed,
                m_damages          = new HitData.DamageTypes { m_blunt = 0f },  // set per-ritual
                m_useDurability    = false,
                m_maxDurability    = float.MaxValue,
                m_blockPower       = fenrirShared.m_blockPower,
                m_deflectionForce  = fenrirShared.m_deflectionForce,
                m_timedBlockBonus  = fenrirShared.m_timedBlockBonus,
                m_attack           = fenrirShared.m_attack,
                m_secondaryAttack  = fenrirShared.m_secondaryAttack,
                m_animationState   = fenrirShared.m_animationState,
                m_attackForce      = fenrirShared.m_attackForce,
                m_backstabBonus    = fenrirShared.m_backstabBonus,
            };

            ItemData = new ItemDrop.ItemData
            {
                m_stack   = 1,
                m_quality = 1,
                m_shared  = kinShared,
            };

            // Register for split-skill XP/damage (Unarmed × Rockery, 50/50).
            // Key is m_shared.m_name (the $item_* localization key) per
            // the existing SplitSkillWeapons convention.
            Plugin.SplitSkillWeapons[ItemName] = Skills.RockerySkill.SkillType;

            Log.Info("KinFist.Build: ready (block/parry from FistFenrirClaw)");
        }

        // Called from SE_StoneKin.Initialize at tier 3. Updates the
        // shared base blunt damage so the next bare-fist hit by the
        // local player uses the new value.
        //
        // m_shared.m_damages is the SHARED base across both primary and
        // secondary attacks — each attack scales it by its own
        // m_damageMultiplier on swing. So this single setter affects
        // both attacks proportionally (just like vanilla weapons), and
        // any future stance-swapped secondary attack still picks up
        // the same base.
        public static void SetBluntDamage(float damage)
        {
            if (ItemData?.m_shared == null) return;
            ItemData.m_shared.m_damages.m_blunt = damage;
            Log.Debug($"KinFist: blunt damage set to {damage:F1}");
        }
    }
}
