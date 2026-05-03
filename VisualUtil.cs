using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath
{
    public static class VisualUtil
    {
        // Reads a sprite's pixels through a RenderTexture blit. Works regardless
        // of whether the source texture is marked readable (vanilla atlases are not).
        private static Color[] ReadSpritePixels(Sprite src)
        {
            var srcTex = src.texture;
            var rect = src.textureRect;
            int x = (int)rect.x, y = (int)rect.y, w = (int)rect.width, h = (int)rect.height;

            var rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(srcTex, rt);
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(srcTex.width, srcTex.height, TextureFormat.ARGB32, false);
            readable.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels(x, y, w, h);
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
