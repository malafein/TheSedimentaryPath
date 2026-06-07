using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    // Shared "is this position standing on the world's scanned summit?" test,
    // used by the reach-peak pilgrimage (MovementTracker) and the place-a-Watcher
    // -at-peak feat (PlaceMysteriousRockPatch). Tolerances are deliberately tight
    // so it requires actually reaching the peak: within PeakHeightToleranceM of
    // the summit height AND PeakRadiusM of its (x,z). Height alone matches any
    // tall mountain, so both gates are required.
    public static class PeakArrival
    {
        public const float PeakHeightToleranceM = 4f;
        public const float PeakRadiusM          = 4f;

        public static bool IsAtPeak(Vector3 pos)
        {
            if (!WorldData.ScanComplete || WorldData.MaxMountainElevation <= 0f) return false;
            if (pos.y < WorldData.MaxMountainElevation - PeakHeightToleranceM) return false;

            float dx = pos.x - WorldData.MaxMountainXZ.x;
            float dz = pos.z - WorldData.MaxMountainXZ.y;
            return dx * dx + dz * dz <= PeakRadiusM * PeakRadiusM;
        }
    }
}
