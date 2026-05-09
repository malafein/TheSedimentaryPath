using System.Collections.Generic;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    /// <summary>
    /// Attached to every Player. Only the local player runs detection and credit logic.
    ///
    /// Watching timeline (one cycle = RpcInterval seconds):
    ///   Cycle 0  : silent — no bond, no credit, no message
    ///   Cycle 1+ : primary bond visible, primary credit granted, HUD message shown once
    ///   Cycle 2+ : secondary bonds spread out stochastically, reduced credit granted
    ///
    /// The current cycle count is written to the player ZDO so all clients can read it
    /// and render the same visuals deterministically.
    ///
    /// When the primary target is a Vine segment, credit is also propagated to every
    /// connected segment via BFS flood fill.
    /// </summary>
    public class VineWatcher : MonoBehaviour
    {
        private Player _player;
        private float _watchTimer;
        private ZNetView _currentTarget;
        private bool _sessionMessageShown;
        private GameObject _bondEffectToPlant;
        private GameObject _bondEffectToPlayer;
        private Transform _chestBone;

        private const float WatchRadius     = 5f;
        private const float FacingThreshold = 0.7f;
        private const float RpcInterval     = 10f;

        // Vine segment BFS: m_size is 1.5m; 2m gives tolerance for slight offsets.
        private const float VineSegmentStep = 2f;

        private readonly Collider[] _colliders      = new Collider[20];
        private readonly Collider[] _bfsColliders   = new Collider[20];
        private readonly Collider[] _secColliders   = new Collider[16];
        private static int s_mask;

        private const string WatchTargetZdoKey = "sedimentarypath_watching_vine";
        private const string WatchCycleZdoKey  = "sedimentarypath_vine_cycle";

        // Secondary bonds — all clients compute these from the primary position.
        // A secondary is "unlocked" at a given cycle via a deterministic hash so every
        // client agrees without any additional ZDO traffic.
        private const float SecondaryRadius      = 5f;
        private const float SecondaryCreditFactor = 0.5f;
        private const float SecondaryScanInterval = 2f;
        private const float SecondarySpreadChance = 0.50f; // per secondary per eligible cycle

        private readonly List<ZNetView>                   _secondaryTargets    = new List<ZNetView>();
        private readonly Dictionary<ZNetView, GameObject> _secondaryBondFx     = new Dictionary<ZNetView, GameObject>();
        private float _secondaryScanTimer;

        private static readonly string[] WatchMessages =
        {
            "You observe. The vine notices.",
            "Still. Watchful. The vine grows.",
            "A peaceful pursuit, vine observation.",
            "You sit together in comfortable silence.",
            "Riveting stuff, this vine.",
            "Growing. Slowly. Magnificently.",
            "You could be doing anything. You chose this.",
            "Some watch paint dry. You watch vines. Respect.",
        };

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _player = GetComponent<Player>();
        }

        private void OnDestroy()
        {
            StopBondEffect();
        }

        private void Update()
        {
            if (_player == null) return;
            ZNetView nview = _player.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            // Owner processes detection, XP, and credit, then writes state to ZDO.
            if (nview.IsOwner())
                UpdateLocalPlayerLogic(zdo);

            // All clients render visuals from ZDO state.
            UpdateVisuals(zdo);
        }

        // ── Owner-only logic ─────────────────────────────────────────────────────

        private void UpdateLocalPlayerLogic(ZDO zdo)
        {
            if (!_player.IsSitting())
            {
                ResetLocalWatch(zdo);
                return;
            }

            ZNetView target = FindWatchTarget();
            if (target == null)
            {
                ResetLocalWatch(zdo);
                return;
            }

            if (target != _currentTarget)
            {
                _currentTarget = target;
                _watchTimer    = 0f;
                _sessionMessageShown = false;
                _secondaryTargets.Clear();
                _secondaryScanTimer = SecondaryScanInterval; // trigger immediate scan

                zdo.Set(WatchTargetZdoKey, target.GetZDO()?.m_uid.ToString() ?? "");
                zdo.Set(WatchCycleZdoKey, 0);
            }

            _watchTimer += Time.deltaTime;
            if (_watchTimer >= RpcInterval)
            {
                _watchTimer -= RpcInterval;
                OnWatchTick(target, zdo);
            }
        }

        private void ResetLocalWatch(ZDO zdo)
        {
            if (_currentTarget != null || !string.IsNullOrEmpty(zdo.GetString(WatchTargetZdoKey)))
            {
                _watchTimer          = 0f;
                _currentTarget       = null;
                _sessionMessageShown = false;
                _secondaryTargets.Clear();
                zdo.Set(WatchTargetZdoKey, "");
                zdo.Set(WatchCycleZdoKey, 0);
            }
        }

        private void OnWatchTick(ZNetView target, ZDO playerZdo)
        {
            int cycle = playerZdo.GetInt(WatchCycleZdoKey) + 1;
            playerZdo.Set(WatchCycleZdoKey, cycle);

            if (target.IsValid())
            {
                float skillFactor   = _player.GetSkillFactor(VinerySkill.SkillType);
                float maxGrowthTime = GetMaxGrowthTime(target);
                float credit        = VinerySkill.CreditPerTick * skillFactor * maxGrowthTime;

                target.InvokeRPC("RPC_AddVineryCredit", credit);

                if (target.GetComponent<Vine>() != null)
                    PropagateToVineStructure(target, credit);

                // Secondary credit starts on cycle 2, only for deterministically unlocked targets
                if (cycle >= 2)
                    CreditSecondaryTargets(skillFactor, cycle);
            }

            _player.RaiseSkill(VinerySkill.SkillType, VinerySkill.WatchXP);

            if (!_sessionMessageShown)
            {
                _sessionMessageShown = true;
                string msg = WatchMessages[Random.Range(0, WatchMessages.Length)];
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, $"<color=yellow>{msg}</color>");
            }

            Log.Debug($"VineWatcher: cycle {cycle} — credit sent to {target.GetZDO()?.m_uid}, awarded {VinerySkill.WatchXP} Vinery XP");
        }

        private void CreditSecondaryTargets(float skillFactor, int cycle)
        {
            foreach (ZNetView secondary in _secondaryTargets)
            {
                if (!secondary.IsValid()) continue;
                if (!IsSecondaryUnlocked(secondary, cycle)) continue;

                float credit = VinerySkill.CreditPerTick * skillFactor
                    * GetMaxGrowthTime(secondary) * SecondaryCreditFactor;
                secondary.InvokeRPC("RPC_AddVineryCredit", credit);
                Log.Debug($"VineWatcher: secondary credit → {secondary.GetZDO()?.m_uid} ({credit:F2}s)");
            }
        }

        // ── Visuals (all clients) ─────────────────────────────────────────────────

        private void UpdateVisuals(ZDO zdo)
        {
            string targetIdStr = zdo.GetString(WatchTargetZdoKey);
            if (string.IsNullOrEmpty(targetIdStr))
            {
                StopBondEffect();
                return;
            }

            string[] parts = targetIdStr.Split(':');
            GameObject targetObj = null;
            if (parts.Length == 2 && long.TryParse(parts[0], out long u) && uint.TryParse(parts[1], out uint i))
            {
                ZDOID targetId = new ZDOID(u, i);
                targetObj = ZNetScene.instance?.FindInstance(targetId);
            }

            if (targetObj == null)
            {
                StopBondEffect();
                return;
            }

            int cycle = zdo.GetInt(WatchCycleZdoKey);

            // Throttled secondary target scan — runs for all clients so the list is
            // ready for both bond visuals and (owner-only) credit.
            _secondaryScanTimer += Time.deltaTime;
            if (_secondaryScanTimer >= SecondaryScanInterval)
            {
                _secondaryScanTimer = 0f;
                RefreshSecondaryTargets(targetObj);
                Log.Debug($"VineWatcher: secondary scan — {_secondaryTargets.Count} target(s), {CountUnlockedSecondaries(cycle)} unlocked at cycle {cycle}");
            }

            // Primary bond: visible from cycle 1 onward.
            if (cycle < 1)
            {
                StopBondEffect();
                return;
            }


            if (_bondEffectToPlant == null || _bondEffectToPlayer == null)
                StartBondEffect();

            if (_bondEffectToPlant != null && _bondEffectToPlayer != null)
            {
                Vector3 startPos = GetPlayerEndpoint();
                Vector3 endPos   = targetObj.transform.position + Vector3.up * 0.4f;

                _bondEffectToPlant.transform.position = startPos;
                _bondEffectToPlant.transform.LookAt(endPos);
                _bondEffectToPlayer.transform.position = endPos;
                _bondEffectToPlayer.transform.LookAt(startPos);

                float distance = Vector3.Distance(startPos, endPos);
                float t        = Time.time;

                float pulsePlant  = (Mathf.Sin(t * 0.5f + Mathf.Sin(t * 0.15f)) + 1f) * 0.5f;
                float pulsePlayer = (Mathf.Sin(t * 0.6f + 10f + Mathf.Sin(t * 0.2f)) + 1f) * 0.5f;

                Color green = new Color(0.0f, 1.0f, 0.4f, 1.0f);
                Color blue  = new Color(0.0f, 0.5f, 1.0f, 1.0f);

                UpdateEffectInstance(_bondEffectToPlant,  Color.Lerp(green, blue, pulsePlant),  distance);
                UpdateEffectInstance(_bondEffectToPlayer, Color.Lerp(green, blue, pulsePlayer), distance);
            }

            // Secondary bonds: visible from cycle 2, spreading stochastically.
            if (cycle >= 2)
                UpdateSecondaryBondVisuals(targetObj, cycle);
            else
                StopSecondaryBonds();
        }

        private void UpdateSecondaryBondVisuals(GameObject primaryObj, int cycle)
        {
            Vector3 primaryPos = primaryObj.transform.position + Vector3.up * 0.4f;

            // Remove bonds for targets no longer in range or not yet unlocked at this cycle
            var toRemove = new List<ZNetView>();
            foreach (ZNetView t in _secondaryBondFx.Keys)
                if (!_secondaryTargets.Contains(t) || !IsSecondaryUnlocked(t, cycle))
                    toRemove.Add(t);
            foreach (ZNetView t in toRemove)
            {
                if (_secondaryBondFx[t] != null) Destroy(_secondaryBondFx[t]);
                _secondaryBondFx.Remove(t);
            }

            // Add bonds for newly unlocked targets
            foreach (ZNetView secondary in _secondaryTargets)
            {
                if (!IsSecondaryUnlocked(secondary, cycle)) continue;
                if (_secondaryBondFx.ContainsKey(secondary)) continue;

                GameObject fx = CreateSecondaryBondEffect(primaryPos);
                if (fx != null) _secondaryBondFx[secondary] = fx;
            }

            // Update positions and colors every frame
            float time = Time.time;
            foreach (KeyValuePair<ZNetView, GameObject> pair in _secondaryBondFx)
            {
                ZNetView secondary = pair.Key;
                GameObject fx      = pair.Value;
                if (fx == null || !secondary.IsValid()) continue;

                Vector3 secondaryPos = secondary.transform.position + Vector3.up * 0.4f;
                fx.transform.position = primaryPos;
                fx.transform.LookAt(secondaryPos);

                float distance = Vector3.Distance(primaryPos, secondaryPos);

                // Per-target offset so they don't all pulse in sync
                float offset   = (float)(secondary.GetZDO().m_uid.UserID & 0xFF) * 0.025f;
                float pulse    = (Mathf.Sin(time * 0.4f + offset) + 1f) * 0.5f;
                Color dimGreen = new Color(0.0f, 0.75f, 0.3f, 1.0f);
                Color dimTeal  = new Color(0.0f, 0.50f, 0.7f, 1.0f);

                UpdateEffectInstance(fx, Color.Lerp(dimGreen, dimTeal, pulse * 0.8f), distance);
            }
        }

        // ── Secondary target helpers ─────────────────────────────────────────────

        private void RefreshSecondaryTargets(GameObject primaryObj)
        {
            _secondaryTargets.Clear();
            ZNetView primaryNView = primaryObj.GetComponent<ZNetView>();
            Vector3 center = primaryObj.transform.position;

            int count = Physics.OverlapSphereNonAlloc(center, SecondaryRadius, _secColliders, s_mask);
            for (int i = 0; i < count; i++)
            {
                if (_secColliders[i] == null) continue;

                // Only Plant or watchable Pickable — Vine segments handled by BFS
                Plant plant = _secColliders[i].GetComponentInParent<Plant>();
                Pickable pickable = null;
                if (plant == null)
                {
                    pickable = _secColliders[i].GetComponentInParent<Pickable>();
                    if (pickable != null && (!VinerySkill.IsVineryWatchable(pickable)
                            || pickable.GetComponentInParent<Vine>() != null))
                        pickable = null;
                }

                GameObject root = plant?.gameObject ?? pickable?.gameObject;
                if (root == null) continue;

                ZNetView nview = root.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || nview == primaryNView) continue;

                if (!_secondaryTargets.Contains(nview))
                    _secondaryTargets.Add(nview);
            }
        }

        private int CountUnlockedSecondaries(int cycle)
        {
            int n = 0;
            foreach (ZNetView t in _secondaryTargets)
                if (IsSecondaryUnlocked(t, cycle)) n++;
            return n;
        }

        /// <summary>
        /// Deterministic unlock: same result on every client for a given secondary + cycle.
        /// A secondary becomes permanently unlocked once it passes any cycle's roll.
        /// </summary>
        private static bool IsSecondaryUnlocked(ZNetView secondary, int currentCycle)
        {
            if (currentCycle < 2 || !secondary.IsValid()) return false;
            ZDOID uid = secondary.GetZDO().m_uid;
            for (int c = 2; c <= currentCycle; c++)
            {
                uint h = (uint)uid.UserID * 2654435761u ^ (uint)c * 2246822519u;
                if (h % 100 < (uint)(SecondarySpreadChance * 100)) return true;
            }
            return false;
        }

        // ── Bond effect construction ─────────────────────────────────────────────

        private void StartBondEffect()
        {
            StopBondEffect();

            GameObject extPrefab = ZNetScene.instance?.GetPrefab("piece_workbench_ext1");
            if (extPrefab == null) return;

            StationExtension ext = extPrefab.GetComponent<StationExtension>();
            if (ext?.m_connectionPrefab == null) return;

            Vector3 startPos = GetPlayerEndpoint();
            _bondEffectToPlant  = Instantiate(ext.m_connectionPrefab, startPos, Quaternion.identity);
            _bondEffectToPlayer = Instantiate(ext.m_connectionPrefab, startPos, Quaternion.identity);
            InitializeEffectInstance(_bondEffectToPlant);
            InitializeEffectInstance(_bondEffectToPlayer);
        }

        private GameObject CreateSecondaryBondEffect(Vector3 pos)
        {
            GameObject extPrefab = ZNetScene.instance?.GetPrefab("piece_workbench_ext1");
            if (extPrefab == null) return null;

            StationExtension ext = extPrefab.GetComponent<StationExtension>();
            if (ext?.m_connectionPrefab == null) return null;

            GameObject fx = Instantiate(ext.m_connectionPrefab, pos, Quaternion.identity);
            InitializeEffectInstance(fx, isSecondary: true);
            return fx;
        }

        private void InitializeEffectInstance(GameObject effectObj, bool isSecondary = false)
        {
            LineConnect lc = effectObj.GetComponent<LineConnect>();
            if (lc != null) Destroy(lc);

            LineRenderer lr = effectObj.GetComponentInChildren<LineRenderer>();
            if (lr != null) lr.enabled = false;

            foreach (ParticleSystem ps in effectObj.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = ps.main;
                main.startSizeMultiplier *= isSecondary ? 0.08f : 0.12f;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= isSecondary ? 0.45f : 0.75f;

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    renderer.material = new Material(renderer.sharedMaterial);
            }

            GameObject lightObj = new GameObject("BondLight");
            lightObj.transform.SetParent(effectObj.transform, false);
            Light bondLight     = lightObj.AddComponent<Light>();
            bondLight.type      = LightType.Point;
            bondLight.range     = isSecondary ? 1.2f : 2f;
            bondLight.intensity = isSecondary ? 0.2f : 0.4f;
            bondLight.color     = new Color(0.0f, 1.0f, 0.4f, 1.0f);
            bondLight.shadows   = LightShadows.None;
        }

        private void UpdateEffectInstance(GameObject effectObj, Color currentColor, float distance)
        {
            foreach (ParticleSystem ps in effectObj.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = currentColor;

                float speed = main.startSpeed.constant;
                if (speed > 0.01f)
                    main.startLifetime = distance / speed;

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer?.material != null)
                {
                    if (renderer.material.HasProperty("_EmissionColor"))
                        renderer.material.SetColor("_EmissionColor", currentColor * 2f);
                    if (renderer.material.HasProperty("_Color"))
                        renderer.material.SetColor("_Color", currentColor);
                }
            }

            Light bondLight = effectObj.GetComponentInChildren<Light>();
            if (bondLight != null) bondLight.color = currentColor;
        }

        private void StopBondEffect()
        {
            if (_bondEffectToPlant  != null) Destroy(_bondEffectToPlant);
            if (_bondEffectToPlayer != null) Destroy(_bondEffectToPlayer);
            _bondEffectToPlant  = null;
            _bondEffectToPlayer = null;
            StopSecondaryBonds();
        }

        private void StopSecondaryBonds()
        {
            foreach (GameObject fx in _secondaryBondFx.Values)
                if (fx != null) Destroy(fx);
            _secondaryBondFx.Clear();
            _secondaryTargets.Clear();
        }

        // ── Utilities ────────────────────────────────────────────────────────────

        private Vector3 GetPlayerEndpoint()
        {
            if (_chestBone == null && _player != null)
            {
                Animator animator = _player.GetComponentInChildren<Animator>();
                if (animator != null)
                    _chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            return _chestBone != null
                ? _chestBone.position + Vector3.up * 0.25f
                : _player.transform.position + Vector3.up * 1.1f;
        }

        private float GetMaxGrowthTime(ZNetView target)
        {
            Plant plant = target.GetComponent<Plant>();
            if (plant != null) return plant.m_growTimeMax;

            Pickable pickable = target.GetComponent<Pickable>();
            if (pickable != null) return pickable.m_respawnTimeMinutes * 60f;

            Vine vine = target.GetComponent<Vine>();
            if (vine != null) return vine.m_growTime;

            return 4500f;
        }

        /// <summary>
        /// BFS flood fill from the watched vine segment. Adjacent segments are always
        /// ~m_size (1.5m) apart; stepping at VineSegmentStep (2m) visits only the
        /// same structure.
        /// </summary>
        private void PropagateToVineStructure(ZNetView root, float credit)
        {
            var visited  = new HashSet<ZNetView> { root };
            var frontier = new Queue<Vector3>();
            frontier.Enqueue(root.transform.position);

            while (frontier.Count > 0)
            {
                Vector3 pos   = frontier.Dequeue();
                int count     = Physics.OverlapSphereNonAlloc(pos, VineSegmentStep, _bfsColliders, s_mask);
                for (int i = 0; i < count; i++)
                {
                    if (_bfsColliders[i] == null) continue;
                    Vine seg = _bfsColliders[i].GetComponentInParent<Vine>();
                    if (seg == null) continue;
                    ZNetView segView = seg.GetComponent<ZNetView>();
                    if (segView == null || !segView.IsValid() || visited.Contains(segView)) continue;
                    visited.Add(segView);
                    frontier.Enqueue(seg.transform.position);
                    segView.InvokeRPC("RPC_AddVineryCredit", credit);
                }
            }
        }

        private ZNetView FindWatchTarget()
        {
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small");

            Vector3 playerPos = _player.transform.position;
            Vector3 forward   = _player.transform.forward;

            int count = Physics.OverlapSphereNonAlloc(playerPos, WatchRadius, _colliders, s_mask);

            float bestDot    = FacingThreshold;
            ZNetView bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                if (_colliders[i] == null) continue;

                Vine vine = _colliders[i].GetComponentInParent<Vine>();
                Plant plant = vine == null ? _colliders[i].GetComponentInParent<Plant>() : null;
                Pickable pickable = null;
                if (vine == null && plant == null)
                {
                    pickable = _colliders[i].GetComponentInParent<Pickable>();
                    if (pickable != null && (!VinerySkill.IsVineryWatchable(pickable)
                            || pickable.GetComponentInParent<Vine>() != null))
                        pickable = null;
                }

                GameObject root = vine?.gameObject ?? plant?.gameObject ?? pickable?.gameObject;
                if (root == null) continue;

                Vector3 dir = (root.transform.position - playerPos).normalized;
                float dot   = Vector3.Dot(forward, dir);
                if (dot <= bestDot) continue;

                ZNetView nview = root.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    bestDot     = dot;
                    bestTarget  = nview;
                }
            }

            return bestTarget;
        }
    }
}
