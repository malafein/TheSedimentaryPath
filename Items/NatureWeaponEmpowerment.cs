using System.Collections.Generic;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Vinery empowers the vanilla Ashlands "Nature" (jade) weapons — the vine
    // answers those who watched it grow, whoever forged the handle. Each jade
    // weapon chance-applies the Ashlands rooted hold on hit; the wielder's Vinery
    // skill now raises that chance above the authored base.
    //
    // The scaling rides the same per-swing hook as the vinery weapons' effect
    // routing (HumanoidStartAttackPatch): m_attackStatusEffectChance is read when
    // the HitData is built — at the melee hit trigger or projectile fire, after
    // StartAttack — and only on the attacker's owning client, so a per-swing write
    // to the (client-local) shared data is MP-safe.
    //
    // The Staff of the Wilds is the exception: it has no on-swing proc — it
    // SUMMONS tentaroots, and their attacks carry their own (smaller) immobilize
    // chance. Its bonus therefore rides the summons: the summoner's skill factor
    // is stamped into each tentaroot's ZDO at spawn (SpawnAbilityPatch +
    // CharacterAwakePatch), and read back per-swing on whichever client owns the
    // creature when it attacks — ownership can migrate away from the summoner.
    public static class NatureWeaponEmpowerment
    {
        // The jade-gem weapons with the on-hit rooted proc. Vanilla naming is
        // inconsistent: the spear keeps an underscore, and the bow's jade variant
        // is "Root" rather than "Nature".
        private static readonly string[] NatureWeaponPrefabs =
        {
            "AxeBerzerkrNature",
            "MaceEldnerNature",
            "SwordNiedhoggNature",
            "THSwordSlayerNature",
            "SpearSplitner_Nature",
            "CrossbowRipperNature",
            "BowAshlandsRoot",
        };

        // Staff of the Wilds summon chain.
        public const string SummonSpawnPrefab  = "staff_greenroots_spawn";
        public const string SummonPrefab       = "staff_greenroots_tentaroot";
        public const string SummonAttackPrefab = "staff_greenroots_tentaroot_attack";

        // Tuning: flat bonus added across Vinery 0→100 on top of the authored base
        // (weapons 20%, tentaroots 10%) — each proc doubles at skill 100. The
        // summon bonus is half the weapon bonus, keeping the vanilla 10-vs-20
        // scale between the staff's roots and the handheld jades.
        private const float WeaponBonusChance = 0.20f;
        private const float SummonBonusChance = 0.10f;

        // Summoner's Vinery skill factor (0-1), stamped into each tentaroot's ZDO
        // at spawn so the proc scales by the SUMMONER even if creature ownership
        // migrates to another client before it attacks.
        public static readonly int SummonerFactorZdoKey =
            "TSP_summoner_vinery".GetStableHashCode();

        // Authored base proc chance per jade weapon, keyed by m_name: inventory
        // items are built via Unity Instantiate (Inventory.AddItem), which clones
        // the whole [Serializable] SharedData — a held item's shared is a per-item
        // COPY, never the prefab's instance, so reference identity can't be used
        // (same reason Plugin.StanceWeapons keys by m_name). Captured on FIRST
        // sight only as a cheap guard against ever treating a mutated value as
        // base; per-swing writes only ever touch the per-item copies.
        private static readonly Dictionary<string, float> WeaponBaseChance =
            new Dictionary<string, float>();

        private static string _summonAttackName;
        private static float _summonBaseChance;

        // Landing-time note of the local caster's skill factor, consumed when the
        // spawn coroutine's tentaroots Awake moments later (see StampSummonedRoot).
        // Setup fires when the staff PROJECTILE LANDS (Projectile.SpawnOnHit passes
        // m_owner through), not at cast — so several shots in flight simply re-arm
        // the note as each lands, always with the same caster's factor. The window
        // only needs to outlast the spawn coroutine's own delays; generous slack
        // costs nothing (the stamp guards on ZDO ownership).
        private static float _pendingSummonFactor;
        private static float _pendingSummonExpiry = -1f;
        private const float PendingSummonWindow = 30f;

        // Idempotent; called from ObjectDBPatch on every ObjectDB.Awake.
        public static void Build(ObjectDB db)
        {
            foreach (string name in NatureWeaponPrefabs)
            {
                ItemDrop.ItemData.SharedData shared = db?.GetItemPrefab(name)
                    ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared == null)
                {
                    Log.Warn($"NatureWeaponEmpowerment: item '{name}' not found — not empowered");
                    continue;
                }
                if (shared.m_attackStatusEffect == null)
                {
                    Log.Warn($"NatureWeaponEmpowerment: '{name}' has no attack status effect — not empowered");
                    continue;
                }
                if (!WeaponBaseChance.ContainsKey(shared.m_name))
                    WeaponBaseChance[shared.m_name] = shared.m_attackStatusEffectChance;
                Log.Debug($"NatureWeaponEmpowerment: '{name}' ({shared.m_name}) " +
                    $"base={WeaponBaseChance[shared.m_name]:0.###} SE={shared.m_attackStatusEffect.name}");
            }

            // The tentaroot's attack item is a creature-held item, not necessarily
            // in ObjectDB — fall back to the ZNetScene prefab.
            GameObject summonAttack = db?.GetItemPrefab(SummonAttackPrefab)
                ?? ZNetScene.instance?.GetPrefab(SummonAttackPrefab);
            ItemDrop.ItemData.SharedData summonShared = summonAttack
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (summonShared == null)
            {
                Log.Warn($"NatureWeaponEmpowerment: '{SummonAttackPrefab}' not found — staff summons not empowered");
            }
            else if (_summonAttackName == null)
            {
                _summonAttackName = summonShared.m_name;
                _summonBaseChance = summonShared.m_attackStatusEffectChance;
                Log.Debug($"NatureWeaponEmpowerment: '{SummonAttackPrefab}' base={_summonBaseChance:0.###} " +
                    $"SE={summonShared.m_attackStatusEffect?.name ?? "<none>"}");
            }
        }

        // Per-swing (from HumanoidStartAttackPatch, local player only): scale the
        // jade's proc chance by the wielder's Vinery skill. No-op for any weapon
        // not in the jade set.
        public static void EmpowerPlayerSwing(ItemDrop.ItemData.SharedData shared)
        {
            if (!WeaponBaseChance.TryGetValue(shared.m_name, out float baseChance)) return;

            Player player = Player.m_localPlayer;
            float factor  = player != null ? player.GetSkillFactor(VinerySkill.SkillType) : 0f;
            shared.m_attackStatusEffectChance = baseChance + WeaponBonusChance * factor;
            Log.Debug($"NatureWeaponEmpowerment: jade swing chance={shared.m_attackStatusEffectChance:0.###} " +
                $"(base {baseChance:0.###}, vinery factor {factor:0.###})");
        }

        // Per-swing (from HumanoidStartAttackPatch, non-player Humanoids): if this
        // is a staff-summoned tentaroot attacking, scale its immobilize chance by
        // the summoner's stamped skill factor. Runs on the creature's owning
        // client — the same client that rolls the proc when the HitData is built.
        public static void EmpowerSummonSwing(Humanoid summon, ItemDrop.ItemData.SharedData shared)
        {
            if (_summonAttackName == null || shared.m_name != _summonAttackName) return;

            ZDO zdo = summon.GetComponent<ZNetView>()?.GetZDO();
            float factor = zdo?.GetFloat(SummonerFactorZdoKey, 0f) ?? 0f;
            shared.m_attackStatusEffectChance = _summonBaseChance + SummonBonusChance * factor;
            Log.Debug($"NatureWeaponEmpowerment: tentaroot swing chance={shared.m_attackStatusEffectChance:0.###} " +
                $"(base {_summonBaseChance:0.###}, summoner factor {factor:0.###})");
        }

        // Landing-time (from SpawnAbilityPatch, on the caster's client): note the
        // caster's skill factor for the tentaroots about to spawn. Anything
        // Awakening past the window is not ours to stamp.
        public static void NoteSummonCast(Player caster)
        {
            _pendingSummonFactor = caster.GetSkillFactor(VinerySkill.SkillType);
            _pendingSummonExpiry = Time.time + PendingSummonWindow;
            Log.Debug($"NatureWeaponEmpowerment: summon cast noted (factor {_pendingSummonFactor:0.###})");
        }

        // Spawn-time (from CharacterAwakePatch): stamp the noted factor into the
        // newly created tentaroot's ZDO. Only the caster's client both holds a
        // live note and owns the fresh ZDO; remote instantiations fail the
        // ownership guard, and an existing stamp is never overwritten (a summon
        // reloaded inside someone else's note window keeps its original
        // summoner's factor).
        public static void StampSummonedRoot(Character summon)
        {
            if (Time.time > _pendingSummonExpiry) return;

            ZNetView nview = summon.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo.GetFloat(SummonerFactorZdoKey, -1f) >= 0f) return;

            zdo.Set(SummonerFactorZdoKey, _pendingSummonFactor);
            Log.Debug($"NatureWeaponEmpowerment: tentaroot stamped (factor {_pendingSummonFactor:0.###})");
        }
    }
}
