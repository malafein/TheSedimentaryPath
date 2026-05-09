using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.World
{
    /// <summary>
    /// Attached to every Player. Only active for the local player.
    /// Detects when the local player is sitting, within range, and the character
    /// model is facing a Vine, Plant, or watchable Pickable. Every 10 seconds of
    /// watching, awards Vinery XP and sends growth credit to the server via RPC.
    ///
    /// When the target is a Vine segment, credit is propagated to all connected
    /// segments via BFS flood fill stepping at the vine's segment spacing.
    ///
    /// While watching, a bond effect sweeps sparkles from the watcher to the vine
    /// every few seconds to visualize the connection.
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

        private const float WatchRadius = 5f;
        private const float FacingThreshold = 0.7f; // ~45 degree cone
        private const float RpcInterval = 10f;

        // Vine segment BFS: m_size is 1.5m; 2m gives tolerance for slight offsets.
        private const float VineSegmentStep = 2f;

        private readonly Collider[] _colliders = new Collider[20];
        private readonly Collider[] _bfsColliders = new Collider[20];
        private static int s_mask;
        private const string WatchTargetZdoKey = "sedimentarypath_watching_vine";

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

            // 1. Local Player processes detection logic, XP, and growth ticks, then broadcasts their target to ZDO
            if (nview.IsOwner())
            {
                UpdateLocalPlayerLogic(zdo);
            }

            // 2. ALL clients render the visual effects using the synchronized ZDO
            UpdateVisuals(zdo);
        }

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

            // Target changed
            if (target != _currentTarget)
            {
                _currentTarget = target;
                _watchTimer = 0f;
                _sessionMessageShown = false;
                
                // Broadcast target ZDOID string to all clients
                string idStr = target.GetZDO()?.m_uid.ToString() ?? "";
                zdo.Set(WatchTargetZdoKey, idStr);
            }

            _watchTimer += Time.deltaTime;
            if (_watchTimer >= RpcInterval)
            {
                _watchTimer -= RpcInterval;
                OnWatchTick(target);
            }
        }

        private void ResetLocalWatch(ZDO zdo)
        {
            string currentVal = zdo.GetString(WatchTargetZdoKey);
            if (_currentTarget != null || !string.IsNullOrEmpty(currentVal))
            {
                _watchTimer = 0f;
                _currentTarget = null;
                _sessionMessageShown = false;
                zdo.Set(WatchTargetZdoKey, "");
            }
        }

        private void UpdateVisuals(ZDO zdo)
        {
            string targetIdStr = zdo.GetString(WatchTargetZdoKey);

            if (string.IsNullOrEmpty(targetIdStr))
            {
                StopBondEffect();
                return;
            }

            // Look up ZDOID manually
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

            if (_bondEffectToPlant == null || _bondEffectToPlayer == null)
            {
                StartBondEffect();
            }

            if (_bondEffectToPlant != null && _bondEffectToPlayer != null)
            {
                Vector3 startPos = GetPlayerEndpoint();
                Vector3 endPos = targetObj.transform.position + Vector3.up * 0.4f;

                _bondEffectToPlant.transform.position = startPos;
                _bondEffectToPlant.transform.LookAt(endPos);
                
                _bondEffectToPlayer.transform.position = endPos;
                _bondEffectToPlayer.transform.LookAt(startPos);

                float distance = Vector3.Distance(startPos, endPos);

                float t = Time.time;
                // Slower, independently fluctuating pulses for a peaceful effect
                float pulsePlant = (Mathf.Sin(t * 0.5f + Mathf.Sin(t * 0.15f)) + 1f) * 0.5f;
                float pulsePlayer = (Mathf.Sin(t * 0.6f + 10f + Mathf.Sin(t * 0.2f)) + 1f) * 0.5f;

                Color green = new Color(0.0f, 1.0f, 0.4f, 1.0f);
                Color blue = new Color(0.0f, 0.5f, 1.0f, 1.0f);

                Color colorPlant = Color.Lerp(green, blue, pulsePlant);
                Color colorPlayer = Color.Lerp(green, blue, pulsePlayer);

                UpdateEffectInstance(_bondEffectToPlant, colorPlant, distance);
                UpdateEffectInstance(_bondEffectToPlayer, colorPlayer, distance);
            }
        }

        private Vector3 GetPlayerEndpoint()
        {
            if (_chestBone == null && _player != null)
            {
                Animator animator = _player.GetComponentInChildren<Animator>();
                if (animator != null) _chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            return _chestBone != null ? _chestBone.position + Vector3.up * 0.25f : _player.transform.position + Vector3.up * 1.1f;
        }

        private void OnWatchTick(ZNetView target)
        {
            if (target.IsValid())
            {
                float skillFactor = _player.GetSkillFactor(VinerySkill.SkillType);
                float maxGrowthTime = GetMaxGrowthTime(target);
                float credit = VinerySkill.CreditPerTick * skillFactor * maxGrowthTime;

                target.InvokeRPC("RPC_AddVineryCredit", credit);

                // BFS: credit every segment of the same vine structure
                if (target.GetComponent<Vine>() != null)
                    PropagateToVineStructure(target, credit);
            }

            _player.RaiseSkill(VinerySkill.SkillType, VinerySkill.WatchXP);

            // Show flavor message once per watch session, not every tick.
            // Center type fades in/out silently — no sound, no queue.
            if (!_sessionMessageShown)
            {
                _sessionMessageShown = true;
                string msg = WatchMessages[Random.Range(0, WatchMessages.Length)];
                MessageHud.instance?.ShowMessage(
                    MessageHud.MessageType.Center,
                    $"<color=yellow>{msg}</color>");
            }

            Log.Debug($"VineWatcher: watch tick — sent credit to {target.GetZDO()?.m_uid}, awarded {VinerySkill.WatchXP} Vinery XP");
        }

        private float GetMaxGrowthTime(ZNetView target)
        {
            float maxTime = 4500f; // Default fallback

            Plant plant = target.GetComponent<Plant>();
            if (plant != null)
            {
                return plant.m_growTimeMax;
            }

            Pickable pickable = target.GetComponent<Pickable>();
            if (pickable != null)
            {
                return pickable.m_respawnTimeMinutes * 60f;
            }

            Vine vine = target.GetComponent<Vine>();
            if (vine != null)
            {
                return vine.m_growTime;
            }

            return maxTime;
        }

        /// <summary>
        /// BFS flood fill from the watched vine segment. Vine segments have no
        /// parent reference in their ZDO — they are independent GameObjects.
        /// Adjacent segments are always exactly m_size (1.5m) apart, so stepping
        /// at VineSegmentStep (2m) visits only segments touching the same structure.
        /// </summary>
        private void PropagateToVineStructure(ZNetView root, float credit)
        {
            var visited = new HashSet<ZNetView> { root };
            var frontier = new Queue<Vector3>();
            frontier.Enqueue(root.transform.position);

            while (frontier.Count > 0)
            {
                Vector3 pos = frontier.Dequeue();
                int count = Physics.OverlapSphereNonAlloc(pos, VineSegmentStep, _bfsColliders, s_mask);
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

        private void StartBondEffect()
        {
            StopBondEffect();

            GameObject extPrefab = ZNetScene.instance?.GetPrefab("piece_workbench_ext1");
            if (extPrefab != null)
            {
                StationExtension ext = extPrefab.GetComponent<StationExtension>();
                if (ext != null && ext.m_connectionPrefab != null)
                {
                    Vector3 startPos = GetPlayerEndpoint();

                    _bondEffectToPlant = Instantiate(ext.m_connectionPrefab, startPos, Quaternion.identity);
                    _bondEffectToPlayer = Instantiate(ext.m_connectionPrefab, startPos, Quaternion.identity);
                    
                    InitializeEffectInstance(_bondEffectToPlant);
                    InitializeEffectInstance(_bondEffectToPlayer);
                }
            }
        }

        private void InitializeEffectInstance(GameObject effectObj)
        {
            // Destroy the LineConnect component so it stops fighting us for control
            LineConnect lc = effectObj.GetComponent<LineConnect>();
            if (lc != null) Destroy(lc);

            // Disable useless LineRenderer if present to save perf
            LineRenderer lr = effectObj.GetComponentInChildren<LineRenderer>();
            if (lr != null) lr.enabled = false;

            ParticleSystem[] pSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in pSystems)
            {
                ParticleSystem.MainModule main = ps.main;
                main.startSizeMultiplier *= 0.12f;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= 0.75f;

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    // Clone the material immediately so we can cleanly tint it below without modifying vanilla objects
                    renderer.material = new Material(renderer.sharedMaterial);
                }
            }

            // Soft ambient glow that pulses with the bond color
            GameObject lightObj = new GameObject("BondLight");
            lightObj.transform.SetParent(effectObj.transform, false);
            Light bondLight = lightObj.AddComponent<Light>();
            bondLight.type      = LightType.Point;
            bondLight.range     = 2f;
            bondLight.intensity = 0.4f;
            bondLight.color     = new Color(0.0f, 1.0f, 0.4f, 1.0f);
            bondLight.shadows   = LightShadows.None;
        }

        private void UpdateEffectInstance(GameObject effectObj, Color currentColor, float distance)
        {
            ParticleSystem[] pSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in pSystems)
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = currentColor;
                
                float speed = main.startSpeed.constant;
                if (speed > 0.01f)
                {
                    main.startLifetime = distance / speed;
                }

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    if (renderer.material.HasProperty("_EmissionColor"))
                        renderer.material.SetColor("_EmissionColor", currentColor * 2f); // Emissive boost
                    if (renderer.material.HasProperty("_Color"))
                        renderer.material.SetColor("_Color", currentColor);
                }
            }

            Light bondLight = effectObj.GetComponentInChildren<Light>();
            if (bondLight != null)
                bondLight.color = currentColor;
        }

        private void StopBondEffect()
        {
            if (_bondEffectToPlant != null) Destroy(_bondEffectToPlant);
            if (_bondEffectToPlayer != null) Destroy(_bondEffectToPlayer);
            
            _bondEffectToPlant = null;
            _bondEffectToPlayer = null;
        }

        private ZNetView FindWatchTarget()
        {
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small");

            Vector3 playerPos = _player.transform.position;

            // Use character model forward so the player must physically face the target,
            // not just aim the camera at it.
            Vector3 forward = _player.transform.forward;

            int count = Physics.OverlapSphereNonAlloc(playerPos, WatchRadius, _colliders, s_mask);

            float bestDot = FacingThreshold;
            ZNetView bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                if (_colliders[i] == null) continue;

                // Priority: Vine > Plant (any cultivated) > watchable Pickable
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
                float dot = Vector3.Dot(forward, dir);
                if (dot <= bestDot) continue;

                ZNetView nview = root.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    bestDot = dot;
                    bestTarget = nview;
                }
            }

            return bestTarget;
        }
    }
}
