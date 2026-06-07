using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Shared per-frame movement sampler for the local player, driven by
    // PlayerMovementPatch (Player.Update postfix). One position delta per frame
    // feeds three feats split by locomotion mode, plus the peak-pilgrimage trial:
    //   - on foot (grounded, not mounted, not on a ship deck) -> The Trodden Path
    //   - riding a tamed mount (Player.IsRiding)               -> Borne by Beasts
    //   - swimming (Player.IsSwimming)                         -> The Long Swim
    //   - grounded altitude high-water-mark                    -> Nearer the Sky
    //   - grounded at the scanned world summit (height + x,z)  -> Nothing Stood Higher
    //
    // The distance feats use the same per-metre accumulator + quiet credit as
    // ShipDistancePatch - throttle the log, not the credit.
    public static class MovementTracker
    {
        // Discard a single-frame horizontal delta larger than this (teleports,
        // portal warps, the load-boundary snap) so they can't bank a huge delta.
        private const float TeleportThresholdM = 30f;

        private static bool    _hasPrev;
        private static Vector3 _prevPos;
        private static float   _walkAcc;   // fractional metres walked, carried between frames
        private static float   _rideAcc;   // fractional metres ridden
        private static float   _swimAcc;   // fractional metres swum

        // Session caches so the common (no-new-record) frame does no journal I/O.
        private static int  _sessionBestAlt = int.MinValue;
        private static bool _peakDone;
        private static bool _peakInit;

        // Reset between worlds/characters so a delta across the load boundary
        // isn't banked and the peak/altitude caches re-seed from the new save.
        public static void Reset()
        {
            _hasPrev        = false;
            _walkAcc        = 0f;
            _rideAcc        = 0f;
            _swimAcc        = 0f;
            _sessionBestAlt = int.MinValue;
            _peakDone       = false;
            _peakInit       = false;
        }

        public static void Sample(Player player)
        {
            if (player == null) return;

            Vector3 cur = player.transform.position;

            // Altitude / peak are high-water-marks - independent of the distance
            // accumulators and the teleport gate (a single sample can't push a
            // high-water-mark past the real terrain height).
            SampleHeight(player, cur);

            if (!_hasPrev)
            {
                _prevPos = cur;
                _hasPrev = true;
                return;
            }

            float dx = cur.x - _prevPos.x;
            float dz = cur.z - _prevPos.z;
            _prevPos = cur;

            float horiz = Mathf.Sqrt(dx * dx + dz * dz);
            if (horiz <= 0f || horiz > TeleportThresholdM) return;

            // Riding a tamed mount -> Borne by Beasts (metres).
            if (player.IsRiding())
            {
                _rideAcc += horiz;
                if (_rideAcc >= 1f)
                {
                    int whole = (int)_rideAcc;
                    _rideAcc -= whole;
                    FeatTracker.RecordEvent(player, Feats.DistanceRidden, whole, quiet: true);
                }
                return;
            }

            // Swimming -> The Long Swim (metres). Checked before the on-foot
            // branch: while swimming IsOnGround is false, so they don't overlap,
            // but this keeps the intent explicit.
            if (player.IsSwimming())
            {
                _swimAcc += horiz;
                if (_swimAcc >= 1f)
                {
                    int whole = (int)_swimAcc;
                    _swimAcc -= whole;
                    FeatTracker.RecordEvent(player, Feats.SwimDistance, whole, quiet: true);
                }
                return;
            }

            // On foot -> The Trodden Path (metres). Exclude a moving ship deck:
            // sea travel is The Salt Path, credited by ShipDistancePatch.
            if (player.IsOnGround() && player.GetStandingOnShip() == null)
            {
                _walkAcc += horiz;
                if (_walkAcc >= 1f)
                {
                    int whole = (int)_walkAcc;
                    _walkAcc -= whole;
                    FeatTracker.RecordEvent(player, Feats.DistanceWalked, whole, quiet: true);
                }
            }
        }

        private static void SampleHeight(Player player, Vector3 cur)
        {
            // Gate on grounded so jumps, falls and launches don't count.
            if (!player.IsOnGround()) return;

            // Dungeons/instanced interiors are generated far above the world
            // (Character.InInterior() == position.y > 3000f), so their y would
            // otherwise bank an absurd high-water-mark. Ignore altitude there.
            if (player.InInterior()) return;

            float seaLevel = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            int altitude = (int)(cur.y - seaLevel);

            // Only touch the journal on a new session high; RecordPersonalBest
            // then dedups against the persisted record.
            if (altitude > _sessionBestAlt)
            {
                _sessionBestAlt = altitude;
                if (altitude > 0)
                    FeatTracker.RecordPersonalBest(player, Feats.HighestAltitude, altitude);
            }

            TryPeak(player, cur);
        }

        // Nothing Stood Higher - fires once when the player stands on the scanned
        // world summit (both high enough and horizontally on it).
        private static void TryPeak(Player player, Vector3 cur)
        {
            if (!_peakInit)
            {
                _peakInit = true;
                _peakDone = JournalData.GetFeat(player, Feats.PeakReached) > 0;
            }
            if (_peakDone) return;
            if (!PeakArrival.IsAtPeak(cur)) return;

            // RecordPersonalBest(..., 1) fires the feat + lore exactly once.
            FeatTracker.RecordPersonalBest(player, Feats.PeakReached, 1);
            _peakDone = true;
            Log.Info($"MovementTracker: reached the world summit at ({cur.x:F0},{cur.z:F0}) y={cur.y:F1}");
        }
    }
}
