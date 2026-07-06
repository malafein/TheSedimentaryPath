using HarmonyLib;
using malafein.Valheim.TheSedimentaryPath.Journal;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Owner-side death detection. Runs as a Prefix so the ZNetView and ZDO
    // are still valid — Character.OnDeath's body calls ZNetScene.Destroy at
    // the end, which invalidates m_nview / IsOwner() for any Postfix.
    //
    // All feat credit decisions are evaluated by every receiving client in
    // AchievementSystem.ResolveDeath. That makes the credit path
    // multiplayer-correct: participating non-owner clients get their own
    // credit instead of being silently dropped.
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class CharacterDeathPatch
    {
        private static readonly AccessTools.FieldRef<Character, HitData> LastHitRef =
            AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");
        private static readonly AccessTools.FieldRef<Character, ZNetView> NviewRef =
            AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");

        public static void Prefix(Character __instance)
        {
            if (__instance == null || __instance is Player) return;

            ZNetView nv = NviewRef(__instance);
            if (nv == null || !nv.IsOwner()) return;
            ZDO zdo = nv.GetZDO();
            if (zdo == null) return;

            HitData lastHit = LastHitRef(__instance);

            long attackerPlayerId    = 0L;
            bool wasThrownTSPStone   = false;
            bool wasTSPRockeryWeapon = false;
            bool wasTSPVineryWeapon  = false;
            if (lastHit != null)
            {
                if (lastHit.GetAttacker() is Player attackerPlayer)
                    attackerPlayerId = attackerPlayer.GetPlayerID();
                wasThrownTSPStone   = AchievementSystem.IsThrownTSPStone(lastHit);
                wasTSPRockeryWeapon = AchievementSystem.IsTSPRockeryWeaponHit(lastHit);
                wasTSPVineryWeapon  = AchievementSystem.IsTSPVineryWeaponHit(lastHit);

                // DoT death: the tick's HitData names neither attacker nor
                // weapon (SE ticks call ApplyDamage with a fresh HitData and
                // SetAttacker is a no-op on the vanilla DoT effects). Recover
                // both from the last direct player hit stamped on the ZDO.
                if (attackerPlayerId == 0L
                    && (lastHit.m_hitType == HitData.HitType.Poisoned
                        || lastHit.m_hitType == HitData.HitType.Burning))
                {
                    attackerPlayerId   = zdo.GetLong(AchievementSystem.ZdoLastHitPlayerHash, 0L);
                    wasTSPVineryWeapon = zdo.GetInt(AchievementSystem.ZdoLastHitVineHash, 0) == 1;
                }
            }

            int  dmgCount = zdo.GetInt(AchievementSystem.ZdoDmgCountHash, 0);
            bool nonstone = zdo.GetInt(AchievementSystem.ZdoNonstoneDmgHash, 0) != 0;
            bool nonKin   = zdo.GetInt(AchievementSystem.ZdoNonKinDmgHash,  0) != 0;

            // No player ever touched this creature — natural death, mob-on-mob,
            // environment. Nothing on any client can credit, so skip the RPC.
            if (attackerPlayerId == 0L && dmgCount == 0) return;

            string prefab = Utils.GetPrefabName(__instance.gameObject);
            bool   isBoss = __instance.IsBoss();

            CreatureDeathRpc.Broadcast(
                __instance.GetZDOID(),
                prefab,
                isBoss,
                attackerPlayerId,
                wasThrownTSPStone,
                wasTSPRockeryWeapon,
                wasTSPVineryWeapon,
                dmgCount,
                nonstone,
                nonKin);
        }
    }
}
