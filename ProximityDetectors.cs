using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    /// <summary>
    /// Lazy cache for SE_Finder (the Wishbone status effect).
    /// Used by both proximity detectors to borrow its pulse EffectLists.
    /// </summary>
    internal static class WishboneEffects
    {
        private static SE_Finder _seFinder;
        private static bool _attempted;

        internal static void Reset()
        {
            _seFinder = null;
            _attempted = false;
        }

        internal static SE_Finder Get()
        {
            if (_attempted) return _seFinder;
            _attempted = true;

            if (ObjectDB.instance == null) return null;

            GameObject wishbone = ObjectDB.instance.GetItemPrefab("Wishbone");
            _seFinder = wishbone?.GetComponent<ItemDrop>()
                                ?.m_itemData?.m_shared?.m_equipStatusEffect as SE_Finder;

            if (_seFinder == null)
                ZLog.LogWarning("[TheSedimentaryPath] WishboneEffects: SE_Finder not found on Wishbone — pulse effects unavailable");
            else
                ZLog.Log("[TheSedimentaryPath] WishboneEffects: cached SE_Finder successfully");

            return _seFinder;
        }
    }

    /// <summary>
    /// Base class for skill-gated proximity detectors.
    ///
    /// Detection radius scales 1:1 with skill level (e.g. level 50 = 50 m),
    /// with a minimum of 5 m so low-level players still get some feedback.
    ///
    /// Skill >= 0.25 (level 25): HUD message when target is in range.
    /// Skill >= 0.50 (level 50): HUD message + Wishbone-style pulse effect.
    /// </summary>
    public abstract class SkillProximityDetector : MonoBehaviour
    {
        protected Player Player;

        private float _pingTimer;
        private float _messageCooldown;   // counts DOWN; fires message when <= 0
        protected float NearestDistance = float.MaxValue;
        protected Vector3 NearestPosition;

        protected const float HudSkillFactor   = 0.25f;
        protected const float PulseSkillFactor = 0.50f;
        protected const float ScanInterval     = 1f;
        protected const float MessageCooldown  = 30f;
        protected const float CloseFrequency   = 1f;
        protected const float DistantFrequency = 5f;
        protected const float RadiusAtUnlock   = 25f; // radius (m) at skill level 25 (unlock threshold)
        protected const float RadiusAtMax      = 50f; // radius (m) at skill level 100
        // Beacon.m_range default — pitch/rate fraction is computed over this inner radius so
        // the feel matches the real Wishbone regardless of our (larger) detection radius.
        protected const float PingFractionRadius = 20f;

        protected abstract bool IsEnabled { get; }
        protected abstract bool IsEffectEnabled { get; }
        protected abstract float GetSkillFactor();
        protected abstract float GetSkillLevel();
        protected abstract float FindNearest(float radius);
        protected abstract string[] Messages { get; }
        protected abstract Color DebugLineColor { get; }

        // Radius starts at 25m when the skill unlocks (level 25) and
        // scales linearly to 50m at level 100.
        protected float GetDetectionRadius() =>
            Mathf.Lerp(RadiusAtUnlock, RadiusAtMax, Mathf.InverseLerp(25f, 100f, GetSkillLevel()));

        protected virtual void Awake()
        {
            Player = GetComponent<Player>();
            InvokeRepeating(nameof(Scan), 0f, ScanInterval);
        }

        private void Scan()
        {
            if (Player == null || Player != global::Player.m_localPlayer) return;
            if (!IsEnabled)
            {
                NearestDistance = float.MaxValue;
                _messageCooldown = 0f;
                return;
            }

            float skillFactor = GetSkillFactor();
            float radius      = GetDetectionRadius();

            if (skillFactor < HudSkillFactor)
            {
                NearestDistance  = float.MaxValue;
                _messageCooldown = 0f;
                return;
            }

            NearestDistance = FindNearest(radius);

            if (Plugin.DebugMode.Value)
            {
                Plugin.DebugLog($"[ProximityDetector:{GetType().Name}] Scan: skillFactor={skillFactor:F2} " +
                                $"radius={radius:F1} nearest={NearestDistance:F1} cooldown={_messageCooldown:F1}");
            }

            if (NearestDistance < radius)
            {
                _messageCooldown -= ScanInterval;
                if (_messageCooldown <= 0f)
                {
                    _messageCooldown = MessageCooldown;
                    string msg = Messages[Random.Range(0, Messages.Length)];

                    Plugin.DebugLog($"[ProximityDetector:{GetType().Name}] Firing HUD message (nearest={NearestDistance:F1}m, radius={radius:F1}m)");

                    MessageHud.instance?.ShowMessage(
                        MessageHud.MessageType.Center,
                        $"<color=yellow>{msg}</color>");
                }
            }
            else
            {
                if (NearestDistance == float.MaxValue && _messageCooldown != 0f)
                    Plugin.DebugLog($"[ProximityDetector:{GetType().Name}] Nothing in range — resetting cooldown (was {_messageCooldown:F1})");

                _messageCooldown = 0f; // reset so next entry fires immediately
                _pingTimer       = 0f;
            }
        }

        private void Update()
        {
            if (Player == null || Player != global::Player.m_localPlayer) return;
            if (!IsEnabled || !IsEffectEnabled) return;
            if (GetSkillFactor() < PulseSkillFactor) return;

            float radius = GetDetectionRadius();
            if (NearestDistance >= radius) return;

            SE_Finder se = WishboneEffects.Get();
            if (se == null) return;

            float fraction = Mathf.Clamp01(NearestDistance / PingFractionRadius);
            float freq     = Mathf.Lerp(CloseFrequency, DistantFrequency, fraction);

            _pingTimer += Time.deltaTime;
            if (_pingTimer >= freq)
            {
                _pingTimer = 0f;
                PlayPulse(se, fraction);
            }
        }

        private void PlayPulse(SE_Finder se, float fraction)
        {
            Vector3   pos = Player.transform.position;
            Quaternion rot = Player.transform.rotation;
            Transform  t   = Player.transform;

            if (fraction < 0.2f)
                se.m_pingEffectNear.Create(pos, rot, t);
            else if (fraction < 0.6f)
                se.m_pingEffectMed.Create(pos, rot, t);
            else
                se.m_pingEffectFar.Create(pos, rot, t);
        }

        // ---------------------------------------------------------------------------
        // Debug line rendering

        private static Material _glMaterial;

        private static Material GetGLMaterial()
        {
            if (_glMaterial != null) return _glMaterial;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite",   0);
            return _glMaterial;
        }

        private void OnRenderObject()
        {
            if (!Plugin.DebugMode.Value) return;
            if (Player == null || Player != global::Player.m_localPlayer) return;
            if (NearestDistance == float.MaxValue) return;

            Material mat = GetGLMaterial();
            mat.SetPass(0);

            Vector3 from = Player.transform.position + Vector3.up * 1.1f;
            Vector3 to   = NearestPosition           + Vector3.up * 0.5f;

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(DebugLineColor);
            GL.Vertex(from);
            GL.Vertex(to);
            GL.End();
            GL.PopMatrix();
        }
    }

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Detects nearby Pickable_StoneRock (the raw material for the Mysterious Rock).
    /// Active when Rockery skill is sufficient.
    /// </summary>
    public class RockeryProximityDetector : SkillProximityDetector
    {
        protected override bool IsEnabled       => Plugin.RockeryProximityAlert.Value;
        protected override bool IsEffectEnabled => Plugin.RockeryProximityEffect.Value;
        protected override Color DebugLineColor => Color.red;

        protected override float GetSkillFactor() =>
            Player?.GetSkillFactor(RockerySkill.SkillType) ?? 0f;

        protected override float GetSkillLevel() =>
            Player?.GetSkillLevel(RockerySkill.SkillType) ?? 0f;

        protected override string[] Messages { get; } =
        {
            "The stone calls to you.",
            "The geological vibes here are immaculate.",
            "You sense the presence of stone.",
            "Something ancient lies close.",
            "You sense the presence of... a really nice rock.",
            "A particularly interesting stone is nearby.",
            "Something heavy and ancient lies close.",
        };

        protected override float FindNearest(float radius)
        {
            Vector3 pos   = Player.transform.position;
            float nearest = float.MaxValue;
            int found     = 0;

            foreach (Pickable pickable in Object.FindObjectsOfType<Pickable>())
            {
                if (pickable.GetPicked()) continue;

                string prefabName = Utils.GetPrefabName(pickable.gameObject);
                if (prefabName != "Pickable_StoneRock") continue;

                float dist = Utils.DistanceXZ(pos, pickable.transform.position);
                if (dist >= radius) continue;

                found++;
                if (dist < nearest)
                {
                    nearest         = dist;
                    NearestPosition = pickable.transform.position;

                    if (Plugin.DebugMode.Value)
                    {
                        ZNetView nv = pickable.GetComponent<ZNetView>();
                        ZDOID uid = nv != null && nv.IsValid() ? nv.GetZDO().m_uid : ZDOID.None;
                        Plugin.DebugLog($"[RockeryProximityDetector] Nearest Pickable_StoneRock: dist={dist:F1}m ZDO={uid}");
                    }
                }
            }

            Plugin.DebugLog($"[RockeryProximityDetector] Scan r={radius:F1}: {found} valid StoneRock(s) in range.");

            if (nearest == float.MaxValue) NearestPosition = Vector3.zero;
            return nearest;
        }
    }

    // ---------------------------------------------------------------------------

    /// <summary>
    /// Detects nearby Vineberry clusters that are ready for harvest.
    /// Active when Vinery skill is sufficient.
    /// </summary>
    public class VineryProximityDetector : SkillProximityDetector
    {
        private enum Category
        {
            None,
            Vine,
            Berry,
            Mushroom,
            Crop,
            Herb
        }

        private Category _nearestCategory = Category.None;
        private static readonly System.Collections.Generic.Dictionary<string, Category> _prefabCategoryCache = new System.Collections.Generic.Dictionary<string, Category>();

        protected override bool IsEnabled       => Plugin.VineryProximityAlert.Value;
        protected override bool IsEffectEnabled => Plugin.VineryProximityEffect.Value;
        protected override Color DebugLineColor => Color.green;

        protected override float GetSkillFactor() =>
            Player?.GetSkillFactor(VinerySkill.SkillType) ?? 0f;

        protected override float GetSkillLevel() =>
            Player?.GetSkillLevel(VinerySkill.SkillType) ?? 0f;

        private static readonly string[] VineMessages =
        {
            "The vine beckons.",
            "Something ripens on the vine.",
            "A cluster awaits your hand.",
            "The vine has been patient. So should you.",
        };

        private static readonly string[] BerryMessages =
        {
            "You smell the sweet scent of wild berries.",
            "A ripe bush is rustling nearby.",
            "Fresh fruit is within reach.",
            "Nature offers a sweet bounty here.",
        };

        private static readonly string[] MushroomMessages =
        {
            "You sense the earthy musk of fungi.",
            "A mushroom cap peeks from the soil nearby.",
            "Spores drift faintly in the air.",
            "The damp earth hides a fungal treasure.",
        };

        private static readonly string[] CropMessages =
        {
            "The soil has yielded a harvest.",
            "Crops are ready for the picking.",
            "You sense agricultural growth.",
            "A bountiful harvest awaits.",
        };

        private static readonly string[] HerbMessages =
        {
            "A sharp, herbal scent catches your attention.",
            "Wild herbs bloom close by.",
            "The flora here holds potent properties.",
            "A delicate blossom stirs in the breeze.",
        };

        private static readonly string[] HarvestMessages =
        {
            "Something ripe awaits.",
            "A harvest is within reach.",
            "Fruit, and patience rewarded.",
            "The forest offers something.",
        };

        protected override string[] Messages
        {
            get
            {
                switch (_nearestCategory)
                {
                    case Category.Vine: return VineMessages;
                    case Category.Berry: return BerryMessages;
                    case Category.Mushroom: return MushroomMessages;
                    case Category.Crop: return CropMessages;
                    case Category.Herb: return HerbMessages;
                    default: return HarvestMessages;
                }
            }
        }

        protected override float FindNearest(float radius)
        {
            Vector3 pos   = Player.transform.position;
            float nearest = float.MaxValue;
            int found     = 0;

            foreach (Pickable pickable in Object.FindObjectsOfType<Pickable>())
            {
                float dist = Utils.DistanceXZ(pos, pickable.transform.position);
                if (dist >= radius) continue;

                bool isPicked    = pickable.GetPicked();
                bool hasVine     = pickable.GetComponentInParent<Vine>() != null;
                bool isWatchable = VinerySkill.IsVineryWatchable(pickable);

                if (hasVine && pickable.m_itemPrefab == null) continue;
                if (isPicked || (!hasVine && !isWatchable)) continue;

                Category cat = Category.None;
                string rawName = pickable.gameObject.name;

                if (!_prefabCategoryCache.TryGetValue(rawName, out cat))
                {
                    string clean = Utils.GetPrefabName(pickable.gameObject).ToLowerInvariant();
                    if (hasVine) cat = Category.Vine;
                    else if (clean.Contains("raspberry") || clean.Contains("blueberry") || clean.Contains("cloudberry") || clean.Contains("vineberry")) cat = Category.Berry;
                    else if (clean.Contains("mushroom") || clean.Contains("magecap") || clean.Contains("jotunpuffs")) cat = Category.Mushroom;
                    else if (clean.Contains("turnip") || clean.Contains("carrot") || clean.Contains("onion") || clean.Contains("barley") || clean.Contains("flax")) cat = Category.Crop;
                    else if (clean.Contains("thistle") || clean.Contains("dandelion")) cat = Category.Herb;
                    else cat = Category.None; // Fallback

                    _prefabCategoryCache[rawName] = cat;
                }

                bool allowed = false;
                switch (cat)
                {
                    case Category.Vine: allowed = Plugin.DetectVines.Value; break;
                    case Category.Berry: allowed = Plugin.DetectBerries.Value; break;
                    case Category.Mushroom: allowed = Plugin.DetectMushrooms.Value; break;
                    case Category.Crop: allowed = Plugin.DetectFieldCrops.Value; break;
                    case Category.Herb: allowed = Plugin.DetectHerbs.Value; break;
                    default: allowed = Plugin.VineryProximityAlert.Value; break;
                }

                if (!allowed) continue;

                found++;
                if (dist < nearest)
                {
                    nearest         = dist;
                    NearestPosition = pickable.transform.position;
                    _nearestCategory = cat;

                    if (Plugin.DebugMode.Value)
                    {
                        ZNetView nv = pickable.GetComponentInParent<ZNetView>()
                                   ?? pickable.GetComponent<ZNetView>();
                        ZDOID uid = nv != null && nv.IsValid() ? nv.GetZDO().m_uid : ZDOID.None;
                        Plugin.DebugLog($"[VineryProximityDetector] Nearest: prefab={rawName} cat={cat} dist={dist:F1}m ZDO={uid}");
                    }
                }
            }

            Plugin.DebugLog($"[VineryProximityDetector] Scan r={radius:F1}: {found} valid targets allowed by config.");

            if (nearest == float.MaxValue) NearestPosition = Vector3.zero;
            return nearest;
        }
    }
}
