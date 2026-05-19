using System;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Routed RPC fired by the ZDO owner of a dying creature so every
    // participating client can credit its own feats locally — fixes the
    // existing owner-only limitation on bosses_defeated / stone_kills /
    // enemies_killed_drunk, and underpins the per-fight feats handled in
    // AchievementSystem.
    //
    // Mirrors VineMaturedRpc shape: register via ZNetScenePatch postfix,
    // pack payload into a ZPackage, route to Everybody.
    public static class CreatureDeathRpc
    {
        public const string Name = "TSP_CreatureDied";

        [Flags]
        private enum DeathFlags
        {
            None                = 0x00,
            IsBoss              = 0x01,
            WasThrownTSPStone   = 0x02,
            WasTSPRockeryWeapon = 0x04,
            NonstoneDamage      = 0x08,
            NonKinDamage        = 0x10,
        }

        public static void Register()
        {
            if (ZRoutedRpc.instance == null)
            {
                Log.Warn($"{Name}: ZRoutedRpc not ready at registration time");
                return;
            }
            ZRoutedRpc.instance.Register<ZPackage>(Name, OnReceive);
            Log.Info($"CreatureDeathRpc: registered {Name} RPC");
        }

        // Called by the owner client from CharacterDeathPatch.
        public static void Broadcast(ZDOID victimId,
                                     string prefab,
                                     bool   isBoss,
                                     long   attackerPlayerId,
                                     bool   wasThrownTSPStone,
                                     bool   wasTSPRockeryWeapon,
                                     int    preDeathDamageCount,
                                     bool   nonstoneDamage,
                                     bool   nonKinDamage)
        {
            if (ZRoutedRpc.instance == null) return;

            DeathFlags flags = DeathFlags.None;
            if (isBoss)              flags |= DeathFlags.IsBoss;
            if (wasThrownTSPStone)   flags |= DeathFlags.WasThrownTSPStone;
            if (wasTSPRockeryWeapon) flags |= DeathFlags.WasTSPRockeryWeapon;
            if (nonstoneDamage)      flags |= DeathFlags.NonstoneDamage;
            if (nonKinDamage)        flags |= DeathFlags.NonKinDamage;

            ZPackage pkg = new ZPackage();
            pkg.Write(victimId);
            pkg.Write(prefab ?? "");
            pkg.Write(attackerPlayerId);
            pkg.Write(preDeathDamageCount);
            pkg.Write((int)flags);

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Name, pkg);
            Log.Debug($"CreatureDeathRpc: broadcast {prefab} (attacker={attackerPlayerId} flags={flags} dmgCount={preDeathDamageCount})");
        }

        private static void OnReceive(long sender, ZPackage pkg)
        {
            if (pkg == null) return;

            ZDOID      victimId         = pkg.ReadZDOID();
            string     prefab           = pkg.ReadString();
            long       attackerPlayerId = pkg.ReadLong();
            int        preDeathDmgCount = pkg.ReadInt();
            DeathFlags flags            = (DeathFlags)pkg.ReadInt();

            bool isBoss               = (flags & DeathFlags.IsBoss)              != 0;
            bool wasThrownTSPStone    = (flags & DeathFlags.WasThrownTSPStone)   != 0;
            bool wasTSPRockeryWeapon  = (flags & DeathFlags.WasTSPRockeryWeapon) != 0;
            bool nonstoneDamage       = (flags & DeathFlags.NonstoneDamage)      != 0;
            bool nonKinDamage         = (flags & DeathFlags.NonKinDamage)        != 0;

            AchievementSystem.ResolveDeath(
                victimId,
                prefab,
                isBoss,
                attackerPlayerId,
                wasThrownTSPStone,
                wasTSPRockeryWeapon,
                preDeathDmgCount,
                nonstoneDamage,
                nonKinDamage);
        }
    }
}
