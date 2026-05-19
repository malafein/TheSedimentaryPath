namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Routed RPC fired when a watched vine sapling matures.
    //
    // Flow:
    //   1. Plant.Grow on the ZDO owner runs (PlantGrowPatch postfix)
    //   2. The owner reads the Plant's TSP_watchers ZDO list and broadcasts
    //      this RPC to Everybody with the list of watcher PlayerIDs packed in
    //      a ZPackage.
    //   3. Each client receives the RPC, checks if its local player's PlayerID
    //      is in the payload, and credits the Patience in Bloom feat if so.
    //
    // PlayerID is stable across sessions, so an offline watcher's credit is
    // simply lost — Phase 1 limitation. They can earn the same feat on future
    // vines they watch.
    public static class VineMaturedRpc
    {
        public const string Name = "TSP_VineMatured";

        // Called from ZNetScenePatch.Postfix after ZNetScene is up. Idempotent.
        public static void Register()
        {
            if (ZRoutedRpc.instance == null)
            {
                Log.Warn($"{Name}: ZRoutedRpc not ready at registration time");
                return;
            }
            ZRoutedRpc.instance.Register<ZPackage>(Name, OnReceive);
            Log.Info($"VineMaturedRpc: registered {Name} RPC");
        }

        private static void OnReceive(long sender, ZPackage pkg)
        {
            if (pkg == null) return;
            Player local = Player.m_localPlayer;
            if (local == null) return;

            long localId = local.GetPlayerID();
            if (localId == 0L) return;

            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                long watcherId = pkg.ReadLong();
                if (watcherId == localId)
                {
                    FeatTracker.RecordEvent(local, Feats.VinesGrown);
                    return;
                }
            }
        }

        // Owner side: pack the watcher list into a ZPackage and broadcast.
        public static void BroadcastMaturation(string watchersCsv)
        {
            if (ZRoutedRpc.instance == null || string.IsNullOrEmpty(watchersCsv)) return;

            string[] parts = watchersCsv.Split(',');
            ZPackage pkg = new ZPackage();
            pkg.Write(parts.Length);
            foreach (string idStr in parts)
            {
                if (long.TryParse(idStr, out long id))
                    pkg.Write(id);
                else
                    pkg.Write(0L);
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Name, pkg);
            Log.Debug($"VineMaturedRpc: broadcast maturation with {parts.Length} watcher(s)");
        }
    }
}
