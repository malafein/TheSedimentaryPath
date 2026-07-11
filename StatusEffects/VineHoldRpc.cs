namespace malafein.Valheim.TheSedimentaryPath.StatusEffects
{
    // Applies the Holdfast retaliation holds (root and/or snare) to an
    // attacker that may be owned by another client.
    //
    // Status effects only take on the target's ZDO owner (SEMan writes are
    // owner-local; vanilla applies on-hit SEs inside RPC_Damage on the
    // owner). The retaliating player is the VICTIM here, so it can't lean
    // on that path — instead this routed RPC mirrors CreatureDeathRpc's
    // shape: broadcast to everybody, and whichever client owns the target
    // applies the effects. Both hold durations are grove-score-scaled per
    // ritual, so the ttls ride the payload and are set on the target's
    // cloned SE instances (never the shared templates). A tier-3 proc
    // carries both — the root's hard hold plus a longer snare that lingers
    // after it releases.
    public static class VineHoldRpc
    {
        public const string Name = "TSP_VineHold";

        private static readonly int SnareHash = VineStatusEffects.SnareEffectName.GetStableHashCode();
        private static readonly int RootHash  = VineStatusEffects.RootEffectName.GetStableHashCode();

        public static bool Register()
        {
            if (ZRoutedRpc.instance == null)
            {
                Log.Warn($"{Name}: ZRoutedRpc not ready at registration time");
                return false;
            }
            ZRoutedRpc.instance.Register<ZPackage>(Name, OnReceive);
            return true;
        }

        // Called on the retaliating player's client. Either ttl may be 0
        // (that hold is skipped). Applies directly when this client owns
        // the attacker (single player, host, local-owned creatures);
        // otherwise routes to the owner.
        public static void Apply(Character target, float rootTtl, float snareTtl)
        {
            if (target == null) return;

            ZNetView nview = target.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            if (nview.IsOwner())
            {
                ApplyLocal(target, rootTtl, snareTtl);
                return;
            }

            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(nview.GetZDO().m_uid);
            pkg.Write(rootTtl);
            pkg.Write(snareTtl);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Name, pkg);
        }

        private static void OnReceive(long sender, ZPackage pkg)
        {
            if (pkg == null) return;

            ZDOID targetId = pkg.ReadZDOID();
            float rootTtl  = pkg.ReadSingle();
            float snareTtl = pkg.ReadSingle();

            UnityEngine.GameObject go = ZNetScene.instance?.FindInstance(targetId);
            Character target = go != null ? go.GetComponent<Character>() : null;
            if (target == null) return;

            ZNetView nview = target.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            ApplyLocal(target, rootTtl, snareTtl);
        }

        private static void ApplyLocal(Character target, float rootTtl, float snareTtl)
        {
            SEMan seman = target.GetSEMan();
            if (seman == null) return;

            if (rootTtl > 0f)  ApplyHold(seman, RootHash,  rootTtl,  "root",  target);
            if (snareTtl > 0f) ApplyHold(seman, SnareHash, snareTtl, "snare", target);
        }

        private static void ApplyHold(
            SEMan seman,
            int hash,
            float ttlSeconds,
            string label,
            Character target)
        {
            StatusEffect se = seman.GetStatusEffect(hash);
            if (se == null)
                se = seman.AddStatusEffect(hash);
            else
                se.ResetTime();

            if (se == null)
            {
                Log.Warn($"VineHoldRpc: could not apply {label} to {target.name}");
                return;
            }

            // The clone belongs to this target's SEMan, so the ttl override
            // never leaks to the shared template.
            se.m_ttl = ttlSeconds;
            Log.Debug($"VineHoldRpc: {label} {ttlSeconds:0.0}s applied to {target.name}");
        }
    }
}
