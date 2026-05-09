using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DateTime = System.DateTime;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    // ── RockShrineComponent ──────────────────────────────────────────────────────
    // Added to every placed Placeable_HardRock instance (via RockPieceAwakePatch).
    //
    // Follows the beehive pattern exactly: ZNet.GetTime() returns a DateTime whose
    // Ticks are stored as a long in the ZDO. Elapsed time accumulates even while
    // the zone is unloaded — on next load we compare stored ticks to current time
    // and process immediately if the interval has passed.
    public class RockShrineComponent : MonoBehaviour
    {
        private const string ZdoKeyLastCheck = "TSP_ShrineLastCheck";

        private ZNetView m_nview;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
        }

        private void Start()
        {
            StartCoroutine(ShrineLoop());
        }

        private IEnumerator ShrineLoop()
        {
            yield return new WaitUntil(() => m_nview != null && m_nview.IsValid());

            while (true)
            {
                if (!m_nview.IsOwner())
                {
                    yield return new WaitForSeconds(5f);
                    continue;
                }

                if (ZNet.instance == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

#if DEBUG
                float baseInterval = Plugin.ShrineIntervalDebug != null && Plugin.ShrineIntervalDebug.Value > 0f
                    ? Plugin.ShrineIntervalDebug.Value
                    : RockShrine.DefaultInterval;
#else
                float baseInterval = RockShrine.DefaultInterval;
#endif

                long lastCheckTicks = m_nview.GetZDO().GetLong(ZdoKeyLastCheck, 0L);
                DateTime currentTime = ZNet.instance.GetTime();

                float jitter = Mathf.Min(RockShrine.JitterRange, baseInterval * 0.5f);

                if (lastCheckTicks == 0L)
                {
                    // First-ever load — record baseline and wait a full interval.
                    m_nview.GetZDO().Set(ZdoKeyLastCheck, currentTime.Ticks);
                    Log.Debug($"RockShrine: Rock at {transform.position} — first load, initial wait {baseInterval:F0}s");
                    yield return new WaitForSeconds(baseInterval + Random.Range(-jitter, jitter));
                    continue;
                }

                DateTime lastCheck = new DateTime(lastCheckTicks);
                float elapsed = (float)(currentTime - lastCheck).TotalSeconds;

                if (elapsed >= baseInterval)
                {
                    Log.Info($"RockShrine: Rock at {transform.position} — processing ({elapsed:F0}s elapsed)");
                    RockShrine.ProcessShrineAt(m_nview);
                    m_nview.GetZDO().Set(ZdoKeyLastCheck, currentTime.Ticks);

                    float waitTime = baseInterval + Random.Range(-jitter, jitter);
                    yield return new WaitForSeconds(Mathf.Max(waitTime, 1f));
                }
                else
                {
                    // Zone reloaded before the interval elapsed — wait out the remainder exactly.
                    float remaining = baseInterval - elapsed;
                    Log.Debug($"RockShrine: Rock at {transform.position} — {remaining:F0}s remaining in interval");
                    yield return new WaitForSeconds(Mathf.Max(remaining, 1f));
                }
            }
        }
    }

    // ── RockShrine ───────────────────────────────────────────────────────────────
    // Server-side shrine logic and client-side RPC handler.
    public static class RockShrine
    {
        public const string RockPrefabName = "Placeable_HardRock";
        public const float DefaultInterval  = 1800f; // one Valheim day
        public const float JitterRange      = 300f;  // ±5 min

        private const string OfferingPrefabName = "Pukeberries";
        private const string GoldPrefabName     = "Coins";
        private const int    BerriesPerBatch    = 10;
        private const int    GoldPerBatch       = 5;
        private const float  ShrineRadius       = 10f;
        public const int    MinScore           = 4;
        public const int    MaxScoreCap        = 20;
        public const float  MinChance          = 0.20f;
        public const float  MaxChance          = 0.80f;
        private const float  SpeechChance       = 0.33f;
        private const float  SpeechCullDist     = 15f;
        private const float  BroadcastRadius    = 30f;

        private static readonly string[] SpeechLines =
        {
            "The Rock provides.",
            "Your faith is measured.",
            "The stone remembers.",
            "Worthy. The cycle continues.",
            "Give, and the weight returns.",
        };

        private static int s_pieceMask;
        private static readonly MethodInfo s_containerSave =
            AccessTools.Method(typeof(Container), "Save");

        // ── Public API ───────────────────────────────────────────────────────────

        public static void RegisterRPCs()
        {
            ZRoutedRpc.instance.Register<ZPackage>("TSP_ShrineConverted", OnShrineConvertedRPC);
            Log.Info("RockShrine: registered TSP_ShrineConverted RPC");
        }

        // Called by RockShrineComponent when it is the ZDO owner and the timer fires.
        public static void ProcessShrineAt(ZNetView rockView)
        {
            Vector3 rockPos = rockView.transform.position;
            int score = ComputeScore(rockPos, rockView.gameObject);

            if (score < MinScore)
            {
                Log.Debug($"RockShrine: Rock at {rockPos} — score={score} (below min {MinScore}), skipping");
                return;
            }

            float t      = Mathf.Clamp01((float)(score - MinScore) / (MaxScoreCap - MinScore));
            float chance = Mathf.Lerp(MinChance, MaxChance, t);
            float roll   = Random.value;

            Log.Info($"RockShrine: Rock at {rockPos} — score={score}, chance={chance:P0}, roll={roll:F3} → {(roll <= chance ? "CONVERT" : "skip")}");

            if (roll > chance)
                return;

            Container chest = FindDonationChest(rockPos);
            if (chest == null)
            {
                Log.Debug($"RockShrine: no eligible chest found near Rock at {rockPos}");
                return;
            }

            PerformConversion(chest, rockView);
        }

        // ── Server-side logic ────────────────────────────────────────────────────

        public static int ComputeScore(Vector3 center, GameObject excludeRock)
        {
            if (s_pieceMask == 0)
                s_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");

            int score   = 0;
            var visited = new HashSet<int>();
            var cols    = Physics.OverlapSphere(center, ShrineRadius, s_pieceMask);

            foreach (var col in cols)
            {
                if (col == null) continue;

                var piece = col.GetComponentInParent<Piece>();
                if (piece == null) continue;

                var root = piece.gameObject;
                if (root == excludeRock) continue;

                int id = root.GetInstanceID();
                if (!visited.Add(id)) continue;

                var wnt = root.GetComponent<WearNTear>();
                if (wnt == null) continue;

                int pts;
                switch (wnt.m_materialType)
                {
                    case WearNTear.MaterialType.Stone:
                    case WearNTear.MaterialType.Marble:
                    case WearNTear.MaterialType.Iron:
                        pts = 2; break;
                    case WearNTear.MaterialType.Wood:
                    case WearNTear.MaterialType.HardWood:
                        pts = 1; break;
                    default:
                        continue;
                }

                Log.Debug($"RockShrine: +{pts}pt — {root.name} ({wnt.m_materialType})");
                score += pts;
            }

            Log.Debug($"RockShrine: total score={score} at {center}");
            return score;
        }

        private static Container FindDonationChest(Vector3 center)
        {
            string offeringName = ResolveOfferingItemName();
            if (string.IsNullOrEmpty(offeringName))
                return null;

            // Use all layers — chest colliders are not on the piece layer
            var cols = Physics.OverlapSphere(center, ShrineRadius);
            foreach (var col in cols)
            {
                if (col == null) continue;

                var container = col.GetComponentInParent<Container>();
                if (container == null) continue;

                var nview = container.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                var inv = container.GetInventory();
                if (inv == null) continue;

                int count = inv.CountItems(offeringName);
                Log.Debug($"RockShrine: chest at {container.transform.position} — {count}x {offeringName}");

                if (count >= BerriesPerBatch)
                    return container;
            }

            return null;
        }

        private static void PerformConversion(Container chest, ZNetView rockView)
        {
            string offeringName = ResolveOfferingItemName();
            if (string.IsNullOrEmpty(offeringName))
            {
                Log.Error("RockShrine: offering item not in ObjectDB, aborting conversion");
                return;
            }

            var inv = chest.GetInventory();
            if (inv.CountItems(offeringName) < BerriesPerBatch)
            {
                Log.Debug("RockShrine: aborted — berries removed before conversion could fire");
                return;
            }

            inv.RemoveItem(offeringName, BerriesPerBatch);
            var added = inv.AddItem(GoldPrefabName, GoldPerBatch, 1, 0, 0L, "");

            if (added == null)
            {
                // Chest full — drop the gold as a ground item near the Rock instead
                Log.Warn("RockShrine: chest full, dropping gold near shrine");
                var coinPrefab = ObjectDB.instance?.GetItemPrefab(GoldPrefabName);
                if (coinPrefab != null)
                    Object.Instantiate(coinPrefab, rockView.transform.position + Vector3.up, Quaternion.identity);
            }

            s_containerSave?.Invoke(chest, null);

            Log.Info($"RockShrine: {BerriesPerBatch}x {OfferingPrefabName} → {GoldPerBatch}x {GoldPrefabName}" +
                     $" | chest at {chest.transform.position} | added to chest: {added != null}");

            string speechLine = Random.value < SpeechChance
                ? SpeechLines[Random.Range(0, SpeechLines.Length)]
                : "";

            BroadcastEffects(chest.transform.position, rockView.transform.position, speechLine);
        }

        private static void BroadcastEffects(Vector3 chestPos, Vector3 rockPos, string speechLine)
        {
            // Always play locally (handles solo and host).
            PlayShrineEffects(chestPos, rockPos, speechLine);

            float sqrRange = BroadcastRadius * BroadcastRadius;
            int sent = 0;

            foreach (var peer in ZNet.instance.GetConnectedPeers())
            {
                if (!peer.IsReady()) continue;
                if ((peer.m_refPos - rockPos).sqrMagnitude > sqrRange) continue;

                var pkg = new ZPackage();
                pkg.Write(chestPos);
                pkg.Write(rockPos);
                pkg.Write(speechLine);
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "TSP_ShrineConverted", pkg);
                sent++;
            }

            Log.Debug($"RockShrine: broadcast to {sent} peer(s), speech='{speechLine}'");
        }

        // ── Client-side RPC handler ──────────────────────────────────────────────

        private static void OnShrineConvertedRPC(long sender, ZPackage pkg)
        {
            Vector3 chestPos  = pkg.ReadVector3();
            Vector3 rockPos   = pkg.ReadVector3();
            string speechLine = pkg.ReadString();

            Log.Debug($"RockShrine: client RPC — chestPos={chestPos}, speech='{speechLine}'");
            PlayShrineEffects(chestPos, rockPos, speechLine);
        }

        private static void PlayShrineEffects(Vector3 chestPos, Vector3 rockPos, string speechLine)
        {
            // Particle effect at chest — small pickable-item sparkle
            var vfx = ZNetScene.instance?.GetPrefab("vfx_pickable_pick")
                   ?? ZNetScene.instance?.GetPrefab("vfx_spawn_small");
            if (vfx != null)
                Object.Instantiate(vfx, chestPos + Vector3.up * 0.5f, Quaternion.identity);
            else
                Log.Debug("RockShrine: no vfx prefab found, skipping VFX");

            // Sound at chest — best-effort against known prefab names; null-safe if unavailable
            var sfx = ZNetScene.instance?.GetPrefab("sfx_coins_placed") ?? ZNetScene.instance?.GetPrefab("sfx_build");
            if (sfx != null)
                Object.Instantiate(sfx, chestPos, Quaternion.identity);
            else
                Log.Debug("RockShrine: no sound prefab found for shrine conversion");

            // Rock speaks (also fires MysteriousRockSpeakPatch → awards Rockery XP to nearby player)
            if (!string.IsNullOrEmpty(speechLine) && Chat.instance != null)
            {
                var rock = FindRockNear(rockPos);
                if (rock != null)
                    Chat.instance.SetNpcText(rock, Vector3.up * 1.5f, SpeechCullDist, 5f, "", speechLine, false);
                else
                    Log.Debug("RockShrine: could not find Rock GameObject for speech");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // Returns m_shared.m_name (the key used by CountItems/RemoveItem).
        private static string ResolveOfferingItemName()
        {
            var name = ObjectDB.instance?.GetItemPrefab(OfferingPrefabName)
                ?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "";
            if (string.IsNullOrEmpty(name))
                Log.Warn($"RockShrine: '{OfferingPrefabName}' not found in ObjectDB");
            return name;
        }

        private static GameObject FindRockNear(Vector3 pos)
        {
            const float maxDistSq = 4f; // 2m radius
            float bestSq = maxDistSq;
            GameObject best = null;

            foreach (var znv in Object.FindObjectsOfType<ZNetView>())
            {
                if (!znv.IsValid()) continue;
                if (Utils.GetPrefabName(znv.gameObject) != RockPrefabName) continue;

                float sq = (znv.transform.position - pos).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = znv.gameObject;
                }
            }

            return best;
        }
    }
}
