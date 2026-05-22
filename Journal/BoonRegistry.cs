using System.Collections.Generic;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Static catalog of every boon. Populated by TSPBoons.RegisterAll()
    // at Plugin.Awake. Parallel to FeatRegistry — kept separate to make
    // future extraction into a library mod cleaner if that ever happens.
    public static class BoonRegistry
    {
        private static readonly Dictionary<string, BoonDef> _byId
            = new Dictionary<string, BoonDef>();

        public static void Register(BoonDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _byId[def.Id] = def;
            Log.Debug($"BoonRegistry: registered '{def.Id}' ({def.Name})");
        }

        public static BoonDef Get(string id)
        {
            _byId.TryGetValue(id, out BoonDef def);
            return def;
        }

        public static IEnumerable<BoonDef> All() => _byId.Values;
    }
}
