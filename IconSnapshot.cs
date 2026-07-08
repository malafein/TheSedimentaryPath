using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    // Renders a prefab's visual subtree to a Sprite for use as an inventory icon.
    // Used by the weapons whose materials are repainted at runtime (albedo
    // overrides) — the vanilla pre-rendered icons no longer match what's held, so
    // we snapshot the real thing and the icon tracks every future texture tweak.
    //
    // The clone is placed far below the world on an unused render layer; an
    // orthographic camera and one directional light see only that layer for a
    // single manual Render() into a temporary RenderTexture. Everything is
    // destroyed before returning.
    public static class IconSnapshot
    {
        private const int SnapshotLayer = 31; // unused by the game
        private static readonly Vector3 FarAway = new Vector3(0f, -2000f, 0f);
        private static int _renders; // unique spot per render — never share a stage
        private static Shader _standardShader;

        // Shader.Find("Standard") returns NULL in Valheim (name lookup only covers
        // the build's shader list), even though the shader is loaded — so fall back
        // to scanning the loaded shaders. Cached; null if truly absent.
        private static Shader StandardShader
        {
            get
            {
                if (_standardShader != null) return _standardShader;
                _standardShader = Shader.Find("Standard");
                if (_standardShader == null)
                {
                    foreach (var s in Resources.FindObjectsOfTypeAll<Shader>())
                    {
                        if (s.name == "Standard") { _standardShader = s; break; }
                    }
                }
                if (_standardShader == null)
                    Log.Warn("IconSnapshot: Standard shader not found — snapshot icons unavailable");
                return _standardShader;
            }
        }

        // Renders `visual` (cloned; the original is untouched) into a square sprite.
        // The visual's longest axis is auto-rotated onto the icon diagonal
        // (bottom-left → top-right), matching vanilla weapon icon framing.
        //   focus — fraction of the diagonal to frame, anchored at the UP-RIGHT end:
        //           1 = whole item; vanilla-style head close-ups are ~0.5-0.7.
        //   flip  — spin the item 180° in the view plane when the business end comes
        //           out pointing down-left.
        //   spin  — degrees of roll about the item's own long axis (the diagonal),
        //           for a 3/4 view instead of a pure profile.
        // Returns null on any failure so the caller can keep its fallback icon.
        public static Sprite Render(
            GameObject visual,
            int size = 128,
            float margin = 1.08f,
            float focus = 1f,
            bool flip = false,
            float spin = 0f,
            string label = null)
        {
            if (visual == null) return null;

            GameObject clone = null;
            GameObject camGo = null;
            GameObject lightGo = null;
            var tempMaterials = new System.Collections.Generic.List<Material>();
            bool fogWas = RenderSettings.fog;
            var ambientModeWas  = RenderSettings.ambientMode;
            var ambientLightWas = RenderSettings.ambientLight;
            try
            {
                clone = Object.Instantiate(visual);
                clone.transform.SetParent(null);
                clone.transform.position = FarAway + Vector3.right * (50f * _renders++);
                clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);
                foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = SnapshotLayer;
                // Sparkle/trail particles don't belong in an icon.
                foreach (var ps in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    ps.enabled = false;

                // Re-materialize the clone in plain Standard, albedo only. Two reasons
                // (both found the hard way, see icon_* dumps from 2026-07-07):
                // Custom/Creature draws NOTHING under a bare manual camera render, and
                // metallic surfaces render black with no environment reflections down
                // here — so every material becomes Standard with _Metallic 0, carrying
                // just the source's _MainTex + _Color.
                Shader standard = StandardShader;
                if (standard == null) return null;
                foreach (var r in clone.GetComponentsInChildren<Renderer>(false))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) continue;
                        var m = new Material(standard)
                        {
                            mainTexture = src.HasProperty("_MainTex") ? src.mainTexture : null,
                            color       = src.HasProperty("_Color")   ? src.color       : Color.white,
                        };
                        m.SetFloat("_Metallic", 0f);
                        m.SetFloat("_Glossiness", 0.2f);
                        tempMaterials.Add(m);
                        mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                }

                Bounds bounds = CalcBounds(clone);
                if (bounds.size == Vector3.zero)
                {
                    Log.Warn($"IconSnapshot[{label}]: no enabled renderers to snapshot");
                    return null;
                }
#if DEBUG
                foreach (var r in clone.GetComponentsInChildren<Renderer>(false))
                    Log.Debug($"IconSnapshot[{label}]: renderer '{r.name}' ({r.GetType().Name}) enabled={r.enabled} bounds={r.bounds.size}");
#endif
                AlignMajorAxisToDiagonal(clone, ref bounds);
                if (flip)
                {
                    clone.transform.RotateAround(bounds.center, Vector3.forward, 180f);
                    bounds = CalcBounds(clone);
                }
                if (spin != 0f)
                {
                    clone.transform.RotateAround(bounds.center, new Vector3(1f, 1f, 0f).normalized, spin);
                    bounds = CalcBounds(clone);
                }

                // With the item on the 45° diagonal its projection is ~square, so one
                // extent drives the square view. focus < 1 slides the view window
                // toward the up-right (tip) corner and shrinks it to match.
                float extent = Mathf.Max(bounds.extents.x, bounds.extents.y);
                float f = Mathf.Clamp(focus, 0.1f, 1f);
                Vector3 viewCenter = bounds.center + new Vector3(extent, extent, 0f) * (1f - f);

                camGo = new GameObject("TSP_IconSnapshotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.enabled         = false; // manual Render() only
                cam.orthographic    = true;
                cam.cullingMask     = 1 << SnapshotLayer;
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane   = 0.05f;
                cam.farClipPlane    = 100f;
                cam.orthographicSize = extent * f * margin;
                cam.transform.position = viewCenter - Vector3.forward * (bounds.extents.z + 5f);
                cam.transform.rotation = Quaternion.identity; // looking along +Z

                // Pointed roughly WITH the view direction so the camera-facing
                // surfaces are the lit ones, tilted for a little shape modelling.
                lightGo = new GameObject("TSP_IconSnapshotLight");
                var light = lightGo.AddComponent<Light>();
                light.type        = LightType.Directional;
                light.cullingMask = 1 << SnapshotLayer;
                light.intensity   = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(30f, -25f, 0f);

                RenderSettings.fog = false;
                // Flat ambient so every shader (Standard AND Custom/Creature) gets a
                // reliable base level; the directional light only adds modelling.
                RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f, 1f);

                Log.Debug($"IconSnapshot[{label}]: bounds size={bounds.size} extent={extent:0.###} focus={f}");

                var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);

#if DEBUG
                // Dump the raw snapshot so icon iterations can be inspected as files
                // (same tsp_texdump folder as the texture dumps).
                if (Plugin.IsDebugMode && !string.IsNullOrEmpty(label))
                {
                    try
                    {
                        string dir = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "tsp_texdump");
                        System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllBytes(
                            System.IO.Path.Combine(dir, $"icon_{label}.png"),
                            tex.EncodeToPNG());
                    }
                    catch (System.Exception e)
                    {
                        Log.Warn($"IconSnapshot[{label}]: debug dump failed: {e.Message}");
                    }
                }
#endif

                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception e)
            {
                // An icon must NEVER take the weapon down with it (it did once:
                // a throw here aborted the whole ObjectDB postfix and neither
                // vinery weapon registered). Fall back to the caller's tinted icon.
                Log.Warn($"IconSnapshot[{label}]: render failed — falling back to tinted icon ({e.GetType().Name}: {e.Message})");
                return null;
            }
            finally
            {
                RenderSettings.fog          = fogWas;
                RenderSettings.ambientMode  = ambientModeWas;
                RenderSettings.ambientLight = ambientLightWas;
                // DestroyImmediate, NOT Destroy: multiple snapshots happen within one
                // ObjectDB.Awake frame, and deferred destruction left the previous
                // weapon's clone alive at the same spot — photobombing the next shot.
                if (clone != null)   Object.DestroyImmediate(clone);
                if (camGo != null)   Object.DestroyImmediate(camGo);
                if (lightGo != null) Object.DestroyImmediate(lightGo);
                foreach (var m in tempMaterials) Object.DestroyImmediate(m);
            }
        }

        // Encapsulates the bounds of every ENABLED renderer (the Share's hidden
        // atgeir geometry stays disabled in the clone and must not skew framing).
        private static Bounds CalcBounds(GameObject root)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return first ? new Bounds(root.transform.position, Vector3.zero) : bounds;
        }

        // Rotates the clone (about the bounds center) so its longest world axis lies
        // along the view-plane diagonal, then recomputes the bounds for framing.
        private static void AlignMajorAxisToDiagonal(GameObject clone, ref Bounds bounds)
        {
            Vector3 size = bounds.size;
            Vector3 major = Vector3.right;
            if (size.y >= size.x && size.y >= size.z) major = Vector3.up;
            else if (size.z >= size.x && size.z >= size.y) major = Vector3.forward;

            Quaternion rot = Quaternion.FromToRotation(major, new Vector3(1f, 1f, 0f).normalized);
            clone.transform.rotation = rot * clone.transform.rotation;
            // Rotating about the transform pivot moves the geometry; re-derive bounds.
            bounds = CalcBounds(clone);
        }
    }
}
