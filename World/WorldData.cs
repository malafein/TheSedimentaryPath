using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    // Per-world geographic facts cached at world-load time. Populated by
    // WorldHeightScanPatch; persists in-process until the next world load.
    //
    // Forward-looking value: lets future TSP features (boon scoring with
    // elevation, mountain-summit pilgrimages, etc.) read max-elevation
    // without re-scanning.
    public static class WorldData
    {
        public static bool    ScanComplete;
        public static float   MaxElevation;            // any biome
        public static float   MaxMountainElevation;    // mountain biome only
        public static Vector2 MaxMountainXZ;           // world-space (x,z) of the mountain peak
    }
}
