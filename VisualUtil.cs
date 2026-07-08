using System.IO;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class VisualUtil
    {
        // Copies any texture into a readable ARGB32 Texture2D through a RenderTexture
        // blit — works on vanilla textures that aren't CPU-readable.
        public static Texture2D ReadableCopy(Texture src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(src.width, src.height, TextureFormat.ARGB32, false);
            readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // Loads a texture-override PNG: a loose file next to the plugin DLL wins
        // (edit → restart, no rebuild — the iteration path), else the embedded
        // Assets copy (the shipped state). Returns null when neither exists.
        public static Texture2D LoadOverrideTexture(string fileName)
        {
            byte[] data = null;
            string source = null;

            string pluginDir = Path.GetDirectoryName(typeof(VisualUtil).Assembly.Location);
            string loosePath = pluginDir != null ? Path.Combine(pluginDir, fileName) : null;
            if (loosePath != null && File.Exists(loosePath))
            {
                data = File.ReadAllBytes(loosePath);
                source = loosePath;
            }
            else
            {
                var assembly = typeof(VisualUtil).Assembly;
                using (var stream = assembly.GetManifestResourceStream("TheSedimentaryPath.Assets." + fileName))
                {
                    if (stream != null)
                    {
                        data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        source = "embedded resource";
                    }
                }
            }
            if (data == null) return null;

            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data))
            {
                Log.Error($"LoadOverrideTexture: failed to decode {fileName}");
                Object.Destroy(tex);
                return null;
            }
            tex.name = fileName;
            Log.Debug($"LoadOverrideTexture: {fileName} loaded from {source}");
            return tex;
        }

        // Reads a sprite's pixels through a RenderTexture blit. Works regardless
        // of whether the source texture is marked readable (vanilla atlases are not).
        private static Color[] ReadSpritePixels(Sprite src)
        {
            var rect = src.textureRect;
            Texture2D readable = ReadableCopy(src.texture);
            var pixels = readable.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            Object.Destroy(readable);
            return pixels;
        }

        private static Sprite BuildSpriteLike(Sprite src, int w, int h, Color[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = src.texture.filterMode;

            var rect = src.textureRect;
            var normalizedPivot = src.pivot / new Vector2(rect.width, rect.height);
            return Sprite.Create(tex, new Rect(0, 0, w, h), normalizedPivot, src.pixelsPerUnit);
        }

        // Multiplies each pixel by the tint color (alpha preserved). This matches
        // how Unity's standard shader applies a material's _Color over _MainTex,
        // so icons tinted this way read the same as the in-world mesh tinted via
        // TintMaterials (which sets material.color = tint).
        public static Sprite TintIcon(Sprite src, Color tint)
        {
            if (src == null || src.texture == null) return src;

            var rect = src.textureRect;
            int w = (int)rect.width, h = (int)rect.height;
            var pixels = ReadSpritePixels(src);

            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color(p.r * tint.r, p.g * tint.g, p.b * tint.b, p.a);
            }

            return BuildSpriteLike(src, w, h, pixels);
        }

        // Centers the source artwork at `scale` size on a transparent canvas of the
        // original dimensions. Inventory scales-to-fit, so the result reads as smaller.
        public static Sprite ShrinkIcon(Sprite src, float scale)
        {
            if (src == null || src.texture == null || scale <= 0f || scale >= 1f) return src;

            var rect = src.textureRect;
            int srcW = (int)rect.width, srcH = (int)rect.height;
            int scaledW = Mathf.Max(1, (int)(srcW * scale));
            int scaledH = Mathf.Max(1, (int)(srcH * scale));

            var srcPixels = ReadSpritePixels(src);
            var canvas = new Color[srcW * srcH]; // transparent by default

            int offsetX = (srcW - scaledW) / 2;
            int offsetY = (srcH - scaledH) / 2;
            for (int y = 0; y < scaledH; y++)
            {
                int sy = (int)((float)y / scaledH * srcH);
                for (int x = 0; x < scaledW; x++)
                {
                    int sx = (int)((float)x / scaledW * srcW);
                    canvas[(offsetY + y) * srcW + (offsetX + x)] = srcPixels[sy * srcW + sx];
                }
            }

            return BuildSpriteLike(src, srcW, srcH, canvas);
        }

        public static Sprite[] TintIcons(Sprite[] src, Color tint)
        {
            if (src == null) return null;
            var result = new Sprite[src.Length];
            for (int i = 0; i < src.Length; i++) result[i] = TintIcon(src[i], tint);
            return result;
        }

        public static Sprite[] ShrinkIcons(Sprite[] src, float scale)
        {
            if (src == null) return null;
            var result = new Sprite[src.Length];
            for (int i = 0; i < src.Length; i++) result[i] = ShrinkIcon(src[i], scale);
            return result;
        }

        // Copies the mesh and materials from the first MeshFilter/MeshRenderer found under
        // `source` into the first MeshFilter/MeshRenderer found under `target`. Returns true
        // on success. Useful for giving a projectile the same appearance as a weapon.
        public static bool CopyMeshInto(GameObject target, GameObject source)
        {
            if (target == null || source == null) return false;

            MeshFilter sourceMF = source.GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceMR = source.GetComponentInChildren<MeshRenderer>();
            if (sourceMF == null || sourceMR == null) return false;

            MeshFilter targetMF = target.GetComponentInChildren<MeshFilter>();
            MeshRenderer targetMR = target.GetComponentInChildren<MeshRenderer>();
            if (targetMF == null || targetMR == null) return false;

            targetMF.sharedMesh      = sourceMF.sharedMesh;
            targetMR.sharedMaterials = sourceMR.sharedMaterials;
            return true;
        }

        // Sets _EmissionColor to black on every material under `root`'s MeshRenderers.
        // Call after TintMaterials so the cloned materials are already unique instances.
        public static void ZeroEmission(GameObject root)
        {
            if (root == null) return;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        // Swaps the albedo texture (and tint) on every material under `root`'s
        // MeshRenderers whose name contains `materialNameContains` (case-insensitive).
        // The stage-2 "de-metalize" lever: an organic donor albedo replaces a metal one,
        // usually with a weaker tint so the donor texture's own detail carries the look.
        // Call after TintMaterials so the cloned materials are already unique instances.
        public static int SwapAlbedo(
            GameObject root,
            string materialNameContains,
            Texture albedo,
            Color tint)
        {
            if (root == null || albedo == null) return 0;
            int count = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (mat.name.IndexOf(materialNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
                    if (mat.HasProperty("_Color"))   mat.color = tint;
                    count++;
                }
            }
            return count;
        }

        // Multiplies `tint` into every ParticleSystem's start color and every particle
        // renderer material under `root`. For retinting a CLONED effect prefab (the
        // materials are cloned here; never call this on a vanilla prefab).
        public static int TintParticles(GameObject root, Color tint)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = MultiplyMinMaxGradient(main.startColor, tint);
                count++;
            }
            foreach (var psr in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var mats = psr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var clone = new Material(mats[i]);
                    if (clone.HasProperty("_TintColor"))
                        clone.SetColor("_TintColor", clone.GetColor("_TintColor") * tint);
                    else if (clone.HasProperty("_Color"))
                        clone.color = clone.color * tint;
                    mats[i] = clone;
                }
                psr.sharedMaterials = mats;
            }
            return count;
        }

        private static ParticleSystem.MinMaxGradient MultiplyMinMaxGradient(
            ParticleSystem.MinMaxGradient src,
            Color tint)
        {
            switch (src.mode)
            {
                case ParticleSystemGradientMode.Color:
                    src.color = src.color * tint;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    src.colorMin = src.colorMin * tint;
                    src.colorMax = src.colorMax * tint;
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    src.gradient = MultiplyGradient(src.gradient, tint);
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    src.gradientMin = MultiplyGradient(src.gradientMin, tint);
                    src.gradientMax = MultiplyGradient(src.gradientMax, tint);
                    break;
            }
            return src;
        }

        private static Gradient MultiplyGradient(Gradient src, Color tint)
        {
            if (src == null) return null;
            var keys = src.colorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                Color c = keys[i].color;
                keys[i].color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }
            var result = new Gradient { mode = src.mode };
            result.SetKeys(keys, src.alphaKeys);
            return result;
        }

        // Shallow-clones an EffectList: a new list with a new entries array, so entries
        // can be added/removed/reordered without touching the vanilla weapon's list.
        // The EffectData entries themselves are still shared — clone an entry before
        // mutating its fields (e.g. to retint or rescale a borrowed effect).
        public static EffectList CloneEffectList(EffectList src)
        {
            if (src?.m_effectPrefabs == null) return src;
            return new EffectList
            {
                m_effectPrefabs = (EffectList.EffectData[])src.m_effectPrefabs.Clone(),
            };
        }

        // Flattens the specular response on every material under `root`'s MeshRenderers:
        // metallic/gloss/smoothness/specular floats go to 0, metal/gloss maps are cleared,
        // metal/spec colors go to black. The vanilla materials keep their metallic response
        // after tinting, which reads as "green-lacquered iron" — matting them lets the tint
        // read as plant matter. Property names are matched generically off the shader so
        // this covers both the Standard shader and Valheim's custom ones.
        // Call after TintMaterials so the cloned materials are already unique instances.
        public static int MatteMaterials(GameObject root)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;
                    var shader = mat.shader;
                    int props = shader.GetPropertyCount();
                    for (int i = 0; i < props; i++)
                    {
                        string name  = shader.GetPropertyName(i);
                        string lower = name.ToLowerInvariant();
                        if (!lower.Contains("metal") && !lower.Contains("gloss") &&
                            !lower.Contains("smooth") && !lower.Contains("spec"))
                            continue;

                        switch (shader.GetPropertyType(i))
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                mat.SetFloat(name, 0f);
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                mat.SetColor(name, Color.black);
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                mat.SetTexture(name, null);
                                break;
                        }
                    }
                    count++;
                }
            }
            return count;
        }

        // DEBUG diagnostics: logs each entry of an EffectList (prefab name + flags), so
        // effect donors/borrows can be identified by their real prefab names in-game.
        public static void DumpEffectList(string label, EffectList list)
        {
#if DEBUG
            if (!Plugin.IsDebugMode) return;
            var prefabs = list?.m_effectPrefabs;
            if (prefabs == null || prefabs.Length == 0)
            {
                Log.Debug($"[FxDump:{label}] <empty>");
                return;
            }
            for (int i = 0; i < prefabs.Length; i++)
            {
                var ed = prefabs[i];
                string prefabName = ed?.m_prefab != null ? ed.m_prefab.name : "<null>";
                Log.Debug($"[FxDump:{label}] [{i}] {prefabName} attach={ed?.m_attach} variant={ed?.m_variant}");
            }
#endif
        }

        // DEBUG diagnostics: logs every renderer/material/shader under `root` with each
        // shader property and its current value, so albedo/normal swap targets can be
        // picked from the real sub-material names instead of guesses.
        public static void DumpMaterials(GameObject root, string label)
        {
#if DEBUG
            if (root == null || !Plugin.IsDebugMode) return;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string path = mr.transform.name;
                for (var t = mr.transform.parent; t != null && t != root.transform; t = t.parent)
                    path = t.name + "/" + path;

                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (mat.shader == null)
                    {
                        Log.Debug($"[MatDump:{label}] {path} → '{mat.name}' (no shader)");
                        continue;
                    }
                    var shader = mat.shader;
                    Log.Debug($"[MatDump:{label}] {path} → '{mat.name}' shader '{shader.name}'");
                    int props = shader.GetPropertyCount();
                    for (int i = 0; i < props; i++)
                    {
                        string name = shader.GetPropertyName(i);
                        var type    = shader.GetPropertyType(i);
                        string value;
                        switch (type)
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                value = mat.GetFloat(name).ToString("0.###");
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                value = mat.GetColor(name).ToString();
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                value = mat.GetVector(name).ToString();
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                var tex = mat.GetTexture(name);
                                value = tex != null ? $"tex '{tex.name}' ({tex.width}x{tex.height})" : "tex <none>";
                                break;
                            default:
                                value = "?";
                                break;
                        }
                        Log.Debug($"[MatDump:{label}]   {name} ({type}) = {value}");
                    }
                }
            }
#endif
        }

        // Clones every material under `root`'s MeshRenderers and sets _Color to `tint`.
        // (Sets, not multiplies — the texture already provides highlight/shadow detail;
        // we just want the overall hue to read as the brew color.)
        public static int TintMaterials(GameObject root, Color tint)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var clone = new Material(mats[i]);
                    if (clone.HasProperty("_Color")) clone.color = tint;
                    mats[i] = clone;
                    count++;
                }
                mr.sharedMaterials = mats;
            }
            return count;
        }
    }
}
