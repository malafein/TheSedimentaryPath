using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Orchestrates feats that need shared per-fight machinery:
    //   one_shot_kills, stone_only_creatures_felled, bosses_stone_only,
    //   stone_only_golem_kills, bosses_unarmored, drunk_pilgrim_bosses,
    //   enemies_killed_by_skip, golem_unarmed_survived.
    //
    // Also owns the death-broadcast credit path for the existing kill feats
    // (stone_kills, enemies_killed_drunk, bosses_defeated) so every
    // participating client — not just the ZDO owner — can credit its own
    // contribution in multiplayer.
    //
    // New per-fight feats can be slotted into ResolveDeath alongside the
    // existing ones; this is a living registry, not a fixed set.
    //
    // Multiplayer model:
    //   - Owner of a creature's ZDO accumulates "global" damage facts on the
    //     ZDO (TSP_dmg_count / TSP_nonstone_dmg / TSP_fight_started). ZDO
    //     replication makes these visible to all loaded clients.
    //   - Each client locally snapshots its own player state the moment it
    //     first observes a boss's TSP_fight_started field set (held in
    //     _bossFights, keyed by boss ZDOID).
    //   - Each client tracks its own damage contribution (HashSet keyed by
    //     boss ZDOID) so it can credit participation even if the Character
    //     is gone by the time the death RPC arrives.
    //   - Owner broadcasts TSP_CreatureDied on Character.OnDeath; every
    //     client routes through ResolveDeath() to evaluate own credit.
    //
    // Performance:
    //   - Tick early-exits when nothing is active.
    //   - Proximity scans use Character.GetAllCharacters() (a static
    //     List<Character>, allocation-free iteration), throttled to 2–4 Hz.
    //   - No per-frame world scans, no GetComponent in the hot path, no LINQ.
    public static class AchievementSystem
    {
        // ── ZDO field keys (owner-written, all-clients-readable) ────────────
        public static readonly int ZdoDmgCountHash     = "TSP_dmg_count".GetStableHashCode();
        public static readonly int ZdoNonstoneDmgHash  = "TSP_nonstone_dmg".GetStableHashCode();
        public static readonly int ZdoNonKinDmgHash    = "TSP_non_kin_dmg".GetStableHashCode();
        public static readonly int ZdoFightStartedHash = "TSP_fight_started".GetStableHashCode();

        // ── TSP-weapon prefab hashes (for owner-side stone attribution) ─────
        // Belt-and-suspenders for melee: HitData.Serialize truncates m_skill
        // to a short, so custom-skill hashes get squeezed to 16 bits on the
        // wire with a (very small) collision risk against other custom-skill
        // mods. The right-hand item hash is a full 32-bit ZDO field with
        // effectively no collision risk, so we cross-check against the four
        // TSP weapon prefab hashes when the attacker still has the weapon in
        // hand (i.e. all melee paths). Thrown SmoothStone (m_consumeItem) is
        // already gone from the right hand by hit-resolution time, so those
        // still rely on the truncated skill match — the only weapon-shape
        // where the residual ~1/65536 risk applies.
        private static readonly int HeftyStoneHash     = "HeftyStone".GetStableHashCode();
        private static readonly int SmoothStoneHash    = "SmoothStone".GetStableHashCode();
        private static readonly int KaldmorkHash       = "Kaldmork".GetStableHashCode();
        private static readonly int DokkbladHash       = "Dokkblad".GetStableHashCode();

        // ── Stone Golem prefab ──────────────────────────────────────────────
        private static readonly int StoneGolemPrefabHash = "StoneGolem".GetStableHashCode();
        private const string       StoneGolemPrefabName = "StoneGolem";

        // ── Tuning constants ────────────────────────────────────────────────
        // Standing Before the Stone proximity. Wiki has golem attacks at 6–8m;
        // 6m is the inner edge — you're within reach of every attack but not
        // glued to the model.
        private const float GolemAggroRange     = 6f;
        private const float GolemAggroRangeSqr  = GolemAggroRange * GolemAggroRange;

        // Boss-fight observation cadence. 4 Hz so a player who walks into an
        // already-started fight observes (and snapshots) within ~250ms —
        // too short a window to react with a drink before commitment.
        private const float BossScanInterval   = 0.25f;
        private const float BossScanRangeSqr   = 100f * 100f;

        private const float GolemScanInterval     = 0.5f;
        private const float StaleSweepInterval    = 10f;
        private const float FightStaleSeconds     = 600f;   // boss fight memory window
        private const float SkipHitStaleSeconds   = 30f;    // skip-kill window before the candidate is forgotten

        // ── Per-client local state ──────────────────────────────────────────
        private class BossFightSnapshot
        {
            public string Prefab;
            public bool   StartedUnarmored;
            public bool   ArmorBroken;
            public bool   StartedDrunk;
            public bool   DrunkBroken;
            public float  LastTouchedTime;
        }

        private static readonly Dictionary<ZDOID, BossFightSnapshot> _bossFights
            = new Dictionary<ZDOID, BossFightSnapshot>();

        // "Did my local player damage this creature at some point during its
        // life?" Survives Character destruction (m_localPlayerHasHit does
        // not). Tracks all creatures, not just bosses — group fights against
        // tough non-boss creatures (golems, trolls, lox, bears) should share
        // stone-only credit among all participants the same way boss feats
        // do. Value is Time.time of the most recent contribution — used to
        // sweep abandoned encounters.
        private static readonly Dictionary<ZDOID, float> _localContributions
            = new Dictionary<ZDOID, float>();

        // "Did my local player land a skipping-stone hit on this victim?"
        // Persists until the death broadcast for that ZDOID arrives (credit
        // and remove) or the stale-sweep evicts it. Replaces an earlier
        // frame-gated single slot that was fragile under RPC delivery latency
        // when attacker and creature-owner are different clients.
        private static readonly Dictionary<ZDOID, float> _skipHitVictims
            = new Dictionary<ZDOID, float>();

        // Cached proximity scans. Refreshed by Tick on the scan cadences.
        private static readonly List<Character> _nearbyBosses = new List<Character>(4);
        private static Character _cachedNearestGolem;

        // Standing Before the Stone running timer (seconds).
        private static float _golemUnarmedTimer;

        // Scratch list reused by SweepStaleFights to avoid foreach-mutation.
        private static readonly List<ZDOID> _sweepBuf = new List<ZDOID>(4);

        private static float _lastBossScan;
        private static float _lastGolemScan;
        private static float _lastStaleSweep;

        // ── Reflection refs for protected vanilla fields ────────────────────
        // Patch-local copies of NviewRef live in CharacterDamagePatch and
        // CharacterDeathPatch — matches the existing convention (LastHitRef,
        // VelRef, etc.). Armor refs are feat-specific and stay here.
        private static readonly AccessTools.FieldRef<Character, ZNetView> NviewRef =
            AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HelmetItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_helmetItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ChestItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_chestItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> LegItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_legItem");

        // ── Owner-side weapon attribution helpers (public for the RPC) ──────

        // HitData.Serialize casts m_skill to short, so custom-skill hashes >
        // 32767 get truncated on the wire. We pre-compute the truncated form
        // of RockerySkill.SkillType and compare hit.m_skill in the 16-bit
        // domain — works whether the HitData was routed via RPC (truncated)
        // or applied locally (full int).
        private static readonly ValheimSkills.SkillType RockerySkillShort =
            (ValheimSkills.SkillType)(short)(int)RockerySkill.SkillType;

        private static bool SkillMatchesRockery(ValheimSkills.SkillType skill)
            => skill == RockerySkill.SkillType
            || skill == RockerySkillShort;

        // True if the killing-blow weapon is a TSP Rockery-family weapon.
        // Two signals:
        //   - Truncated skill match (catches all four weapons including thrown
        //     SmoothStone where the item is consumed before the hit lands)
        //   - Attacker's right-hand item hash matches a TSP weapon (catches
        //     all melee paths with no collision risk)
        public static bool IsTSPRockeryWeaponHit(HitData hit)
        {
            if (hit == null) return false;
            if (SkillMatchesRockery(hit.m_skill)) return true;

            GameObject attackerGo = ZNetScene.instance?.FindInstance(hit.m_attacker);
            if (attackerGo == null) return false;
            ZNetView attackerNv = attackerGo.GetComponent<ZNetView>();
            if (attackerNv == null) return false;
            ZDO zdo = attackerNv.GetZDO();
            if (zdo == null) return false;
            int rightHash = zdo.GetInt(ZDOVars.s_rightItem, 0);
            return rightHash == HeftyStoneHash
                || rightHash == SmoothStoneHash
                || rightHash == KaldmorkHash
                || rightHash == DokkbladHash;
        }

        // True for thrown stone projectiles only (SmoothStone / HeftyStone
        // secondary). Obsidian leap-stance isn't a projectile, so excluded.
        public static bool IsThrownTSPStone(HitData hit)
            => hit != null && hit.m_ranged && SkillMatchesRockery(hit.m_skill);

        // Bare-fist hit: the attacker had nothing equipped in the right hand
        // (no item in m_rightItem → ZDOVars.s_rightItem == 0) AND the hit
        // used the Unarmed skill. The skill check alone is insufficient
        // because fist-weapons (Flesh Rippers, Jotun Bane) also use Unarmed
        // skill but are equipped items.
        public static bool IsBareFistHit(HitData hit)
        {
            if (hit == null) return false;
            if (hit.m_skill != ValheimSkills.SkillType.Unarmed) return false;

            GameObject attackerGo = ZNetScene.instance?.FindInstance(hit.m_attacker);
            if (attackerGo == null) return false;
            ZNetView attackerNv = attackerGo.GetComponent<ZNetView>();
            if (attackerNv == null) return false;
            ZDO zdo = attackerNv.GetZDO();
            if (zdo == null) return false;

            return zdo.GetInt(ZDOVars.s_rightItem, 0) == 0;
        }


        // ── Public API ──────────────────────────────────────────────────────

        // Called from CharacterDamagePatch (Character.RPC_Damage Postfix) on
        // every client where the method ran:
        //   - On the attacker-local client, records "I damaged this boss" so
        //     participation feats can credit later even after Character
        //     destruction.
        //   - On the ZDO owner, accumulates damage facts on the creature's
        //     ZDO so the death broadcast can include them.
        //
        // The caller is responsible for filtering to player-attributed damage
        // before invoking — this avoids the cost of resolving hit.GetAttacker()
        // twice (the patch needs it for the attackerIsLocal decision).
        public static void RecordDamage(Character victim, HitData hit, bool isOwner, bool attackerIsLocal)
        {
            if (victim == null || hit == null) return;

            if (attackerIsLocal)
                _localContributions[victim.GetZDOID()] = Time.time;

            if (!isOwner) return;

            ZNetView nv = NviewRef(victim);
            if (nv == null) return;
            ZDO zdo = nv.GetZDO();
            if (zdo == null) return;

            // damage event count → for one_shot_kills check at death
            int newCount = zdo.GetInt(ZdoDmgCountHash, 0) + 1;
            zdo.Set(ZdoDmgCountHash, newCount);

            // non-stone / non-kin damage flags → each set once, never cleared
            bool isStone = IsTSPRockeryWeaponHit(hit);
            bool isKin   = isStone || IsBareFistHit(hit);

            if (!isStone && zdo.GetInt(ZdoNonstoneDmgHash, 0) == 0)
                zdo.Set(ZdoNonstoneDmgHash, 1);
            if (!isKin && zdo.GetInt(ZdoNonKinDmgHash, 0) == 0)
                zdo.Set(ZdoNonKinDmgHash, 1);

            // boss fight start → set once; first damage from any player
            if (victim.IsBoss() && zdo.GetInt(ZdoFightStartedHash, 0) == 0)
                zdo.Set(ZdoFightStartedHash, 1);
        }

        // Called from SkipStonePatch when a SmoothStone with SkipCount > 0
        // strikes a Character. Records the victim ZDOID as a skip-kill
        // candidate; ResolveDeath consumes by membership when the death
        // broadcast for that ZDOID arrives, or the stale-sweep evicts after
        // SkipHitStaleSeconds if the victim survived the hit.
        public static void NoteSkippingStoneHit(Character victim)
        {
            if (victim == null) return;
            _skipHitVictims[victim.GetZDOID()] = Time.time;
        }

        // Per-frame tick from PlayerUpdatePatch. Performance-critical: must
        // early-exit cheaply when no state is active.
        public static void Tick(Player player, float dt)
        {
            if (player == null) return;
            float now = Time.time;

            if (now - _lastBossScan >= BossScanInterval)
            {
                RefreshNearbyBosses(player.transform.position);
                _lastBossScan = now;
            }
            if (now - _lastGolemScan >= GolemScanInterval)
            {
                RefreshCachedGolem(player.transform.position);
                _lastGolemScan = now;
            }
            if (now - _lastStaleSweep >= StaleSweepInterval)
            {
                SweepStaleFights(now);
                _lastStaleSweep = now;
            }

            // Cheap when both empty: zero work past these scans.
            if (_nearbyBosses.Count > 0) ScanForNewFights(player, now);
            if (_bossFights.Count > 0)    UpdateFightInvariants(player, now);

            // Golem timer evaluated only when a golem is in range.
            UpdateGolemTimer(player, dt);
        }

        // Called from CreatureDeathRpc on every client that receives the
        // owner's death broadcast.
        public static void ResolveDeath(ZDOID victimId,
                                        string prefab,
                                        bool   isBoss,
                                        long   attackerPlayerId,
                                        bool   wasThrownTSPStone,
                                        bool   wasTSPRockeryWeapon,
                                        int    preDeathDamageCount,
                                        bool   nonstoneDamage,
                                        bool   nonKinDamage)
        {
            Player local = Player.m_localPlayer;
            if (local == null)
            {
                _bossFights.Remove(victimId);
                _localContributions.Remove(victimId);
                return;
            }

            long localId = local.GetPlayerID();
            bool localIsKiller = attackerPlayerId != 0L && attackerPlayerId == localId;

            // ── Killing-blow feats ──────────────────────────────────────────
            if (localIsKiller)
            {
                if (wasTSPRockeryWeapon)
                    FeatTracker.RecordEvent(local, Feats.StoneKills);

                if (FeatTracker.IsDrunk(local))
                    FeatTracker.RecordEvent(local, Feats.EnemiesKilledDrunk);

                if (wasThrownTSPStone && preDeathDamageCount <= 1)
                    FeatTracker.RecordEvent(local, Feats.OneShotKills);

                if (wasThrownTSPStone && _skipHitVictims.Remove(victimId))
                    FeatTracker.RecordEvent(local, Feats.EnemiesKilledBySkip);

            }

            // ── Participation feats (any contributor) ───────────────────────
            // Credit anyone who dealt damage to this creature, not just the
            // killing-blow attacker. Boss fights are group events by design;
            // tougher non-boss creatures (golems, trolls, lox, bears) also
            // typically take multiple hits and party support to bring down,
            // especially with the simpler stone weapons.
            //
            // Stone-only / kin-only checks use the ZDO invariant flags only.
            // The killing-blow weapon doesn't gate the credit because a
            // dev-kill or environmental finisher doesn't break the player-
            // damage invariant — only non-stone/non-kin damage from a player
            // does, and that's what the ZDO flags track.
            if (!string.IsNullOrEmpty(prefab) && _localContributions.ContainsKey(victimId))
            {
                if (isBoss)
                {
                    FeatTracker.AddDistinct(local, Feats.BossesDefeated, prefab);

                    if (!nonstoneDamage)
                        FeatTracker.AddDistinct(local, Feats.BossesStoneOnly, prefab);

                    if (_bossFights.TryGetValue(victimId, out BossFightSnapshot snap))
                    {
                        if (snap.StartedUnarmored && !snap.ArmorBroken)
                            FeatTracker.AddDistinct(local, Feats.BossesUnarmored, prefab);
                        if (snap.StartedDrunk && !snap.DrunkBroken)
                            FeatTracker.AddDistinct(local, Feats.DrunkPilgrimBosses, prefab);
                    }
                }
                else
                {
                    if (!nonstoneDamage)
                        FeatTracker.AddDistinct(local, Feats.StoneOnlyCreaturesFelled, prefab);

                    // Brother Felled by Brother — golem-specific, accepts
                    // bare-fist damage alongside stone (kin to the stone).
                    if (prefab == StoneGolemPrefabName && !nonKinDamage)
                        FeatTracker.RecordEvent(local, Feats.KinOnlyGolemKills);
                }
            }

            _bossFights.Remove(victimId);
            _localContributions.Remove(victimId);
            _skipHitVictims.Remove(victimId);
        }

        // Drop all transient state. Called on world unload (ZNetScenePatch).
        public static void ClearAll()
        {
            _bossFights.Clear();
            _localContributions.Clear();
            _skipHitVictims.Clear();
            _nearbyBosses.Clear();
            _cachedNearestGolem = null;
            _golemUnarmedTimer = 0f;
            _lastBossScan = _lastGolemScan = _lastStaleSweep = 0f;
        }

        // ── Internals ───────────────────────────────────────────────────────

        private static void RefreshNearbyBosses(Vector3 playerPos)
        {
            _nearbyBosses.Clear();
            List<Character> all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c == null || c is Player || !c.IsBoss()) continue;
                if ((c.transform.position - playerPos).sqrMagnitude > BossScanRangeSqr) continue;
                _nearbyBosses.Add(c);
            }
        }

        private static void RefreshCachedGolem(Vector3 playerPos)
        {
            _cachedNearestGolem = null;
            float bestSqr = GolemAggroRangeSqr;
            List<Character> all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c == null || c is Player) continue;
                if (!IsStoneGolem(c)) continue;
                float dSqr = (c.transform.position - playerPos).sqrMagnitude;
                if (dSqr < bestSqr)
                {
                    bestSqr = dSqr;
                    _cachedNearestGolem = c;
                }
            }
        }

        private static bool IsStoneGolem(Character c)
        {
            ZNetView nv = NviewRef(c);
            if (nv == null) return false;
            ZDO zdo = nv.GetZDO();
            if (zdo == null) return false;
            return zdo.GetPrefab() == StoneGolemPrefabHash;
        }

        private static void ScanForNewFights(Player player, float now)
        {
            for (int i = 0; i < _nearbyBosses.Count; i++)
            {
                Character boss = _nearbyBosses[i];
                if (boss == null) continue;
                ZNetView nv = NviewRef(boss);
                if (nv == null) continue;
                ZDO zdo = nv.GetZDO();
                if (zdo == null) continue;
                if (zdo.GetInt(ZdoFightStartedHash, 0) != 1) continue;

                ZDOID id = boss.GetZDOID();
                if (_bossFights.ContainsKey(id)) continue;

                string prefab = Utils.GetPrefabName(boss.gameObject);
                BossFightSnapshot snap = new BossFightSnapshot
                {
                    Prefab           = prefab,
                    StartedUnarmored = IsUnarmored(player),
                    StartedDrunk     = FeatTracker.IsDrunk(player),
                    LastTouchedTime  = now,
                };
                _bossFights[id] = snap;
                Log.Debug($"AchievementSystem: snapshot fight {prefab} unarmored={snap.StartedUnarmored} drunk={snap.StartedDrunk}");
            }
        }

        private static void UpdateFightInvariants(Player player, float now)
        {
            bool unarmoredNow = IsUnarmored(player);
            bool drunkNow     = FeatTracker.IsDrunk(player);

            // Class entries — field mutation does not invalidate the enumerator.
            foreach (KeyValuePair<ZDOID, BossFightSnapshot> kvp in _bossFights)
            {
                BossFightSnapshot snap = kvp.Value;
                if (snap.StartedUnarmored && !snap.ArmorBroken && !unarmoredNow)
                {
                    snap.ArmorBroken = true;
                    Log.Debug($"AchievementSystem: armor invariant broken for {snap.Prefab}");
                }
                if (snap.StartedDrunk && !snap.DrunkBroken && !drunkNow)
                {
                    snap.DrunkBroken = true;
                    Log.Debug($"AchievementSystem: drunk invariant broken for {snap.Prefab}");
                }
                snap.LastTouchedTime = now;
            }
        }

        private static void UpdateGolemTimer(Player player, float dt)
        {
            Character g = _cachedNearestGolem;
            if (g == null) { _golemUnarmedTimer = 0f; return; }

            if ((g.transform.position - player.transform.position).sqrMagnitude > GolemAggroRangeSqr)
            {
                _golemUnarmedTimer = 0f;
                return;
            }

            if (!IsUnarmoredAndUnarmed(player))
            {
                _golemUnarmedTimer = 0f;
                return;
            }

            _golemUnarmedTimer += dt;
            int seconds = Mathf.FloorToInt(_golemUnarmedTimer);
            if (seconds > 0)
                FeatTracker.RecordPersonalBest(player, Feats.GolemUnarmedSurvived, seconds);
        }

        private static void SweepStaleFights(float now)
        {
            if (_bossFights.Count > 0)
            {
                _sweepBuf.Clear();
                foreach (KeyValuePair<ZDOID, BossFightSnapshot> kvp in _bossFights)
                {
                    if (now - kvp.Value.LastTouchedTime > FightStaleSeconds)
                        _sweepBuf.Add(kvp.Key);
                }
                for (int i = 0; i < _sweepBuf.Count; i++)
                    _bossFights.Remove(_sweepBuf[i]);
            }

            if (_localContributions.Count > 0)
            {
                _sweepBuf.Clear();
                foreach (KeyValuePair<ZDOID, float> kvp in _localContributions)
                {
                    if (now - kvp.Value > FightStaleSeconds)
                        _sweepBuf.Add(kvp.Key);
                }
                for (int i = 0; i < _sweepBuf.Count; i++)
                    _localContributions.Remove(_sweepBuf[i]);
            }

            if (_skipHitVictims.Count > 0)
            {
                _sweepBuf.Clear();
                foreach (KeyValuePair<ZDOID, float> kvp in _skipHitVictims)
                {
                    if (now - kvp.Value > SkipHitStaleSeconds)
                        _sweepBuf.Add(kvp.Key);
                }
                for (int i = 0; i < _sweepBuf.Count; i++)
                    _skipHitVictims.Remove(_sweepBuf[i]);
            }

            _sweepBuf.Clear();
        }

        // bosses_unarmored rule: helmet + chest + legs empty (cape and utility
        // allowed). Reused by Standing Before the Stone.
        private static bool IsUnarmored(Player player)
            => player != null
            && HelmetItemRef(player) == null
            && ChestItemRef(player)  == null
            && LegItemRef(player)    == null;

        // Both hands must be empty or hold a non-damaging utility (construction
        // hammer, cultivator, lantern). Predicate: item deals damage > 0 (catches
        // weapons including modded ones, torches, pickaxes, and axes — even when
        // pickaxes/axes are ItemType.Tool rather than OneHandedWeapon) OR is a
        // shield (shields deal no damage but represent combat readiness).
        // GetCurrentWeapon() can't be used for the empty-hand check — it returns
        // the unarmed fists ItemData when the right hand is empty, never null.
        private static bool IsCombatEquipped(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return false;
            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) return true;
            return item.m_shared.m_damages.GetTotalDamage() > 0f;
        }

        private static bool IsUnarmed(Player player)
            => player != null
            && !IsCombatEquipped(player.RightItem)
            && !IsCombatEquipped(player.LeftItem);

        private static bool IsUnarmoredAndUnarmed(Player player)
            => IsUnarmored(player) && IsUnarmed(player);
    }
}
