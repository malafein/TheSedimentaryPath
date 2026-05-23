using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Shared UI builder primitives for the journal panel and its tabs.
    // Internal-scoped — only the journal-system code should be using
    // these. Style polish (vanilla sprites / colors) lives in one place
    // when we get to it.
    internal static class JournalUIHelpers
    {
        public static readonly Color BodyTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        public static RectTransform MakeChildRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static TextMeshProUGUI AddText(
            RectTransform rt,
            string text,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.color = BodyTextColor;
            tmp.fontSize = fontSize;
            if (font != null) tmp.font = font;
            return tmp;
        }

        // Build a vertically-scrolling list under `parent`. Returns the
        // Content RectTransform (where list rows go) via the return
        // value, and the outer GameObject (suitable for SetActive
        // toggling) via the out parameter. The Content has a
        // VerticalLayoutGroup + ContentSizeFitter so children stack and
        // the scroll region sizes itself to fit them.
        public static RectTransform BuildScrollableList(
            Transform parent,
            string name,
            out GameObject scrollRoot)
        {
            var rootRt = MakeChildRect(parent, name);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            scrollRoot = rootRt.gameObject;

            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal   = false;
            scrollRect.vertical     = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport masks the content. Needs a Graphic on the same GO
            // for Mask to function.
            var viewportRt = MakeChildRect(rootRt, "Viewport");
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImage = viewportRt.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0);
            var mask = viewportRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content — list rows stack vertically; height fits content.
            var content = MakeChildRect(viewportRt, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot     = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing                = 4;
            vlg.padding                = new RectOffset(5, 5, 5, 5);
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content  = content;

            return content;
        }
    }
}
