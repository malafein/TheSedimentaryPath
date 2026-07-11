using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.World;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // One-shot world-elevation scan run after world load. Caches the
    // world's highest mountain peak into WorldData for consumers like the
    // elevation-aware shrine score (BoonSystem) and any future feature
    // that wants to know the world's summit (mountain-summit pilgrimages,
    // etc.).
    //
    // Three-stage:
    //   Stage 1 (parallel coarse) — partitions the playable circle along
    //             X across N worker threads. Each thread runs its own
    //             coarse grid and maintains its own top-K candidate lists
    //             (no shared state, no locks). Uses NomapPrinter's
    //             GetBiome + GetBiomeHeight pattern — proven thread-safe.
    //   Stage 2 (merge) — main thread folds the per-thread top-K lists
    //             into a global top-K via the same spatial-separation
    //             logic.
    //   Stage 3 (hill-climb) — adaptive-step gradient ascent from each
    //             merged candidate to its true apex (sub-decimeter
    //             precision). Cheap enough to keep on the main thread.
    //
    // Runtime varies by hardware; ~1s on 8 workers for a standard
    // 10km-radius world on the dev box. Lower-end machines may take
    // several seconds; the coroutine yields between rows so the game
    // keeps rendering throughout.
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    public static class WorldHeightScanPatch
    {
        private const float WorldRadius     = 10000f;
        private const float CoarseSpacing   = 25f;
        // Hill-climb: start radius matches coarse grid so the first step
        // can reach any neighbor sampled in stage 1; end radius is sub-
        // decimeter precision on the apex.
        private const float ClimbStartR     = 12.5f;
        private const float ClimbEndR       = 0.1f;
        // Safety cap on hill-climb iterations (normally ~40–60).
        private const int   ClimbMaxIters   = 1000;
        // Wide net so the true highest peak's coarse sample is virtually
        // guaranteed to rank into the candidate list even if a sampling
        // offset slightly underestimates its true height.
        private const int   TopK            = 15;
        // Candidates within this distance of an existing top-K entry are
        // treated as the same peak (taller wins).
        private const float SeparationSqr   = 1000f * 1000f;
        // Worker count cap. ProcessorCount on modern machines reports
        // logical cores (often 8–32); 8 is plenty for this workload and
        // avoids saturating the box during world-load.
        private const int   MaxWorkers      = 8;
        // Main-thread poll cadence while waiting for workers.
        private const float WorkerPollInterval = 0.25f;

        private struct Candidate
        {
            public float Height;
            public float X;
            public float Z;
        }

        private class WorkerContext
        {
            public float XMin;
            public float XMax;
            public List<Candidate> Any;
            public List<Candidate> Mtn;
            public int   CoarseSamples;
            public volatile bool Done;
            public Exception Error;
        }

        public static void Postfix()
        {
            if (Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(RunScan());
        }

        private static IEnumerator RunScan()
        {
            while (WorldGenerator.instance == null || Player.m_localPlayer == null)
                yield return new WaitForSeconds(0.5f);

            // Let world-load settle before we hammer GetBiomeHeight.
            yield return new WaitForSeconds(1f);

            float startTime = Time.realtimeSinceStartup;

            int workerCount = Math.Min(Environment.ProcessorCount, MaxWorkers);
            if (workerCount < 1) workerCount = 1;

            // Partition the X range across workers. Strips are not equal-
            // area (corners of the bounding square are outside the world
            // circle), but each worker's inner-loop skips out-of-circle
            // samples cheaply, so the imbalance is negligible.
            float perWorker = (WorldRadius * 2f) / workerCount;
            var contexts = new WorkerContext[workerCount];
            var threads  = new Thread[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                contexts[i] = new WorkerContext
                {
                    XMin = -WorldRadius + i * perWorker,
                    XMax = -WorldRadius + (i + 1) * perWorker,
                    Any  = new List<Candidate>(TopK + 1),
                    Mtn  = new List<Candidate>(TopK + 1),
                };
                WorkerContext ctx = contexts[i];
                threads[i] = new Thread(() => RunWorker(ctx)) { IsBackground = true };
                threads[i].Start();
            }

            Log.Debug($"WorldHeightScan: dispatched {workerCount} worker(s)");

            // Wait for all workers. Coroutine yields on a 250ms cadence so
            // the game keeps rendering smoothly during the scan.
            while (true)
            {
                bool allDone = true;
                for (int i = 0; i < workerCount; i++)
                {
                    if (!contexts[i].Done) { allDone = false; break; }
                }
                if (allDone) break;
                yield return new WaitForSeconds(WorkerPollInterval);
            }

            // ── Surface any worker exceptions ────────────────────────────────
            for (int i = 0; i < workerCount; i++)
            {
                if (contexts[i].Error != null)
                {
                    Log.Error($"WorldHeightScan: worker {i} failed: {contexts[i].Error}");
                }
            }

            // ── Merge per-thread top-K lists ─────────────────────────────────
            var anyMerged = new List<Candidate>(TopK + 1);
            var mtnMerged = new List<Candidate>(TopK + 1);
            int totalCoarse = 0;

            for (int i = 0; i < workerCount; i++)
            {
                totalCoarse += contexts[i].CoarseSamples;
                for (int j = 0; j < contexts[i].Any.Count; j++)
                {
                    Candidate c = contexts[i].Any[j];
                    ConsiderCandidate(anyMerged, c.Height, c.X, c.Z);
                }
                for (int j = 0; j < contexts[i].Mtn.Count; j++)
                {
                    Candidate c = contexts[i].Mtn[j];
                    ConsiderCandidate(mtnMerged, c.Height, c.X, c.Z);
                }
            }

            float coarseElapsed = Time.realtimeSinceStartup - startTime;

            // ── Stage 3 — hill-climb each merged candidate to its true apex ──
            int climbSamples = 0;
            for (int i = 0; i < anyMerged.Count; i++)
            {
                anyMerged[i] = HillClimb(anyMerged[i], mountainOnly: false, out int t);
                climbSamples += t;
                yield return null;
            }
            for (int i = 0; i < mtnMerged.Count; i++)
            {
                mtnMerged[i] = HillClimb(mtnMerged[i], mountainOnly: true, out int t);
                climbSamples += t;
                yield return null;
            }

            Candidate bestAny = PickHighest(anyMerged);
            Candidate bestMtn = PickHighest(mtnMerged);

            float totalElapsed = Time.realtimeSinceStartup - startTime;

            WorldData.ScanComplete         = true;
            WorldData.MaxElevation         = bestAny.Height;
            WorldData.MaxMountainElevation = bestMtn.Height;
            WorldData.MaxMountainXZ        = new Vector2(bestMtn.X, bestMtn.Z);

            Vector3 spawn = Player.m_localPlayer.transform.position;

            Log.Debug(
                $"WorldHeightScan: maxMountain={bestMtn.Height:F2}m at ({bestMtn.X:F1},{bestMtn.Z:F1}) " +
                $"maxAny={bestAny.Height:F2}m elapsed={totalElapsed:F2}s"
            );

            Log.Debug(
                $"WorldHeightScan: detail — workers={workerCount} coarse={totalCoarse} " +
                $"climb={climbSamples} (coarse {coarseElapsed:F2}s) " +
                $"playerSpawn=(y={spawn.y:F1})"
            );

            for (int i = 0; i < mtnMerged.Count; i++)
            {
                Candidate c = mtnMerged[i];
                Log.Debug($"WorldHeightScan: mountain candidate {i}: y={c.Height:F2}m at ({c.X:F1},{c.Z:F1})");
            }
        }

        // Worker body — runs on a background thread. Must only call
        // thread-safe APIs (WorldGenerator.GetBiome / GetBiomeHeight are
        // confirmed safe per NomapPrinter's threaded fill).
        private static void RunWorker(WorkerContext ctx)
        {
            try
            {
                float sqrRadius = WorldRadius * WorldRadius;
                for (float x = ctx.XMin; x < ctx.XMax; x += CoarseSpacing)
                {
                    for (float z = -WorldRadius; z <= WorldRadius; z += CoarseSpacing)
                    {
                        if (x * x + z * z > sqrRadius) continue;

                        Heightmap.Biome biome = WorldGenerator.instance.GetBiome(x, z);
                        float h = WorldGenerator.instance.GetBiomeHeight(biome, x, z, out _);
                        ctx.CoarseSamples++;

                        ConsiderCandidate(ctx.Any, h, x, z);
                        if (biome == Heightmap.Biome.Mountain)
                            ConsiderCandidate(ctx.Mtn, h, x, z);
                    }
                }
            }
            catch (Exception e)
            {
                ctx.Error = e;
            }
            finally
            {
                ctx.Done = true;
            }
        }

        // Top-K with spatial separation. Called from worker threads
        // (single-list ownership per thread, no lock needed) and from the
        // main-thread merge (also single-thread access).
        private static void ConsiderCandidate(List<Candidate> list, float h, float x, float z)
        {
            int   nearestIdx = -1;
            float nearestSqr = SeparationSqr;
            for (int i = 0; i < list.Count; i++)
            {
                float dx = list[i].X - x;
                float dz = list[i].Z - z;
                float dSqr = dx * dx + dz * dz;
                if (dSqr < nearestSqr)
                {
                    nearestSqr = dSqr;
                    nearestIdx = i;
                }
            }

            if (nearestIdx >= 0)
            {
                if (h > list[nearestIdx].Height)
                    list[nearestIdx] = new Candidate { Height = h, X = x, Z = z };
                return;
            }

            list.Add(new Candidate { Height = h, X = x, Z = z });
            if (list.Count > TopK)
            {
                int   lowestIdx = 0;
                float lowestH   = list[0].Height;
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].Height < lowestH)
                    {
                        lowestH   = list[i].Height;
                        lowestIdx = i;
                    }
                }
                list.RemoveAt(lowestIdx);
            }
        }

        // Adaptive-step gradient ascent. Samples 8 neighbors at radius r;
        // walks toward the highest improvement; halves r when no neighbor
        // is higher. Converges to ClimbEndR precision regardless of where
        // the coarse-grid seed landed on the mountain.
        //
        // mountainOnly skips neighbors that aren't in Mountain biome so a
        // mountain-list candidate can't climb across a biome boundary and
        // report a non-mountain apex.
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Harmony", "Harmony003",
            Justification = "HillClimb is a regular helper, not a Harmony patch — the analyzer mis-flags local reads of the 'seed' parameter as patch-parameter modifications.")]
        private static Candidate HillClimb(Candidate seed, bool mountainOnly, out int samplesTaken)
        {
            float bestX = seed.X, bestZ = seed.Z, bestH = seed.Height;
            float r = ClimbStartR;
            int   iters = 0;
            int   samples = 0;
            float sqrRadius = WorldRadius * WorldRadius;

            while (r >= ClimbEndR && iters < ClimbMaxIters)
            {
                iters++;
                bool improved = false;
                float newBestX = bestX, newBestZ = bestZ, newBestH = bestH;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        float nx = bestX + dx * r;
                        float nz = bestZ + dz * r;
                        if (nx * nx + nz * nz > sqrRadius) continue;

                        Heightmap.Biome b = WorldGenerator.instance.GetBiome(nx, nz);
                        if (mountainOnly && b != Heightmap.Biome.Mountain) continue;

                        float h = WorldGenerator.instance.GetBiomeHeight(b, nx, nz, out _);
                        samples++;

                        if (h > newBestH)
                        {
                            newBestH = h;
                            newBestX = nx;
                            newBestZ = nz;
                            improved = true;
                        }
                    }
                }

                if (improved)
                {
                    bestX = newBestX;
                    bestZ = newBestZ;
                    bestH = newBestH;
                }
                else
                {
                    r *= 0.5f;
                }
            }

            samplesTaken = samples;
            return new Candidate { Height = bestH, X = bestX, Z = bestZ };
        }

        private static Candidate PickHighest(List<Candidate> list)
        {
            Candidate best = default;
            best.Height = float.MinValue;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Height > best.Height) best = list[i];
            return best;
        }
    }
}
