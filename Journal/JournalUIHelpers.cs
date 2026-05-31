using System.Text.RegularExpressions;
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

        // ── Compendium-style rich-text palette ───────────────────────────
        // Mirrors the vanilla Compendium's use of colour to separate
        // information: body text stays the default near-white (BodyTextColor);
        // section headers and highlighted values are orange; special category
        // labels are yellow. Shared across tabs so the journal reads uniformly.
        // (Per-section colour tweaks get workshopped as tabs are built.)
        public const string HeaderColorTag = "orange";
        public const string ValueColorTag  = "orange";
        public const string LabelColorTag  = "yellow";

        // Golden accent for Color-typed uses (selected list rows, detail topic).
        // Matches the vanilla window-title / Compendium-topic colour; the
        // rich-text HeaderColorTag handles inline body markup.
        public static readonly Color AccentGold = new Color(1f, 0.718f, 0.36f, 1f);

        // Wrap text in a TMP rich-text colour span (caller's TMP needs richText).
        public static string Colored(string colorTag, string s) => $"<color={colorTag}>{s}</color>";

        // A bold orange section header (e.g. a boon / lore-entry name).
        public static string Header(string s) => $"<b>{Colored(HeaderColorTag, s)}</b>";

        // A yellow category label (e.g. "Current effects:", "Ritual:").
        public static string Label(string s) => Colored(LabelColorTag, s);

        // Highlight numeric values (percentages) in orange, like vanilla tooltips.
        public static string HighlightValues(string s)
            => Regex.Replace(s, @"\d+%", m => Colored(ValueColorTag, m.Value));

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
            // Give body text the vanilla crisp outline (the font's default
            // material has none). Only when using the body font, so the outline
            // material's atlas matches; title/button callers override afterward.
            if (font != null && font == VanillaUI.BodyFont && VanillaUI.BodyMaterial != null)
                tmp.fontSharedMaterial = VanillaUI.BodyMaterial;
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
            // Unity's default scrollSensitivity (1) makes the mouse wheel move
            // the list one pixel per notch — effectively dead. Drive it from the
            // config (re-applied on each Open by JournalUIController so changes
            // take effect without a relog).
            scrollRect.scrollSensitivity = Plugin.JournalScrollSensitivity.Value;

            // Viewport clips the content. Use RectMask2D (rect-based
            // clipping) rather than Mask: stencil-based Mask + a zero-alpha
            // mask graphic culls TextMeshPro content instead of clipping it,
            // which left the whole list invisible even though rows were built.
            // RectMask2D needs no graphic and clips Image + TMP alike. A
            // transparent raycast-target Image stays so drags over empty
            // space still scroll the list.
            // Reserve a gutter on the right for the scrollbar so list rows
            // don't sit under it.
            const float scrollbarWidth = 12f;
            const float scrollbarGap   = 4f;

            var viewportRt = MakeChildRect(rootRt, "Viewport");
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = new Vector2(-(scrollbarWidth + scrollbarGap), 0f);
            var viewportImage = viewportRt.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0);
            viewportRt.gameObject.AddComponent<RectMask2D>();

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
            // Control child sizes explicitly: rows take their preferred height
            // (so text rows grow with content) and fill the width.
            vlg.childControlHeight     = true;
            vlg.childControlWidth      = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content  = content;

            // Visible vanilla scrollbar on the right. If the template can't be
            // resolved the list still scrolls (wheel + drag), just without a bar.
            var scrollbar = VanillaUI.CloneScrollbar(rootRt, scrollbarWidth);
            if (scrollbar != null)
            {
                scrollRect.verticalScrollbar = scrollbar;
                // Hide the bar when content fits (vanilla does the same — see
                // TextsDialog's m_rightScrollbar.activeSelf check). The gutter
                // stays reserved so rows don't reflow when it appears/hides.
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }

            return content;
        }
    }
}
