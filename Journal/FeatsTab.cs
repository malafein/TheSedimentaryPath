using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Feats tab — categorized list with collapsible sections.
    //
    // Each FeatCategory gets a clickable header row whose children are
    // the unlocked feats in that category (any feat with value > 0 is
    // shown; zero-progress feats are hidden per spec). Clicking the
    // header toggles the children's visibility. Expanded state persists
    // for the controller's lifetime in `_expanded`.
    //
    // A single footer at the bottom shows the total count of feats the
    // player hasn't yet discovered — one number, not per-category, per
    // the cult-flavored "mystery not roadmap" spec.
    public class FeatsTab : JournalTab
    {
        public override string Label => "Feats";

        private readonly Dictionary<FeatCategory, bool>             _expanded = new Dictionary<FeatCategory, bool>();
        private readonly Dictionary<FeatCategory, List<GameObject>> _rowsByCategory = new Dictionary<FeatCategory, List<GameObject>>();
        private readonly Dictionary<FeatCategory, TextMeshProUGUI>  _headerLabels = new Dictionary<FeatCategory, TextMeshProUGUI>();

        private static readonly Color CategoryHeaderColor = new Color(0.20f, 0.20f, 0.20f, 0.85f);
        private static readonly Color FeatRowColor        = new Color(0.13f, 0.13f, 0.13f, 0.5f);
        private static readonly Color FooterColor         = new Color(0.70f, 0.70f, 0.70f, 1f);

        private const float CategoryHeaderHeight = 32f;
        private const float FeatRowHeight        = 30f;
        private const float ProgressColumnWidth  = 200f;

        protected override void BuildContent(Transform parent)
        {
            // All categories start expanded.
            foreach (FeatCategory cat in System.Enum.GetValues(typeof(FeatCategory)))
                _expanded[cat] = true;

            BuildScrollableListRoot(parent, "FeatsContent");
        }

        // Base default OnActivated clears ListContent and dispatches here.
        protected override void PopulateRows(Player player)
        {
            _rowsByCategory.Clear();
            _headerLabels.Clear();

            int undiscovered = 0;

            foreach (FeatCategory cat in System.Enum.GetValues(typeof(FeatCategory)))
            {
                var unlocked = new List<FeatDef>();
                foreach (var def in FeatRegistry.ByCategory(cat))
                {
                    int value = ValueFor(def, player);
                    if (value > 0) unlocked.Add(def);
                    else undiscovered++;
                }

                if (unlocked.Count == 0) continue;

                BuildCategoryHeader(cat);
                var rows = new List<GameObject>();
                foreach (var def in unlocked)
                    rows.Add(BuildFeatRow(def, player));
                _rowsByCategory[cat] = rows;

                bool isExpanded = !_expanded.TryGetValue(cat, out bool e) || e;
                if (!isExpanded)
                    foreach (var row in rows)
                        row.SetActive(false);
            }

            if (undiscovered > 0)
                BuildUndiscoveredFooter(undiscovered);
        }

        // ── Row builders ─────────────────────────────────────────────────

        private void BuildCategoryHeader(FeatCategory cat)
        {
            var go = new GameObject(
                $"Category_{cat}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(ListContent, false);

            go.GetComponent<Image>().color = CategoryHeaderColor;
            go.GetComponent<LayoutElement>().preferredHeight = CategoryHeaderHeight;

            var labelRt = JournalUIHelpers.MakeChildRect(go.transform, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10, 0);
            labelRt.offsetMax = new Vector2(-10, 0);
            var label = JournalUIHelpers.AddText(
                labelRt,
                FormatHeader(cat),
                Font,
                fontSize: 16,
                alignment: TextAlignmentOptions.MidlineLeft);
            _headerLabels[cat] = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => ToggleCategory(cat));
        }

        private GameObject BuildFeatRow(FeatDef def, Player player)
        {
            var go = new GameObject(
                $"Feat_{def.Id}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            go.transform.SetParent(ListContent, false);

            go.GetComponent<Image>().color = FeatRowColor;
            go.GetComponent<LayoutElement>().preferredHeight = FeatRowHeight;

            // Name on the left, stretches to the start of the progress column.
            var nameRt = JournalUIHelpers.MakeChildRect(go.transform, "Name");
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(18, 0);
            nameRt.offsetMax = new Vector2(-(ProgressColumnWidth + 8), 0);
            JournalUIHelpers.AddText(
                nameRt,
                def.Name,
                Font,
                fontSize: 14,
                alignment: TextAlignmentOptions.MidlineLeft);

            // Progress on the right, fixed width.
            var progRt = JournalUIHelpers.MakeChildRect(go.transform, "Progress");
            progRt.anchorMin        = new Vector2(1, 0);
            progRt.anchorMax        = new Vector2(1, 1);
            progRt.pivot            = new Vector2(1, 0.5f);
            progRt.sizeDelta        = new Vector2(ProgressColumnWidth, 0);
            progRt.anchoredPosition = new Vector2(-8, 0);
            JournalUIHelpers.AddText(
                progRt,
                FormatProgress(def, player),
                Font,
                fontSize: 13,
                alignment: TextAlignmentOptions.MidlineRight);

            return go;
        }

        private void BuildUndiscoveredFooter(int count)
        {
            var go = new GameObject(
                "UndiscoveredFooter",
                typeof(RectTransform),
                typeof(LayoutElement));
            go.transform.SetParent(ListContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40;

            string msg = count == 1
                ? "There is 1 feat you have not yet found."
                : $"There are {count} feats you have not yet found.";

            var tmp = JournalUIHelpers.AddText(
                (RectTransform)go.transform,
                msg,
                Font,
                fontSize: 13,
                alignment: TextAlignmentOptions.Center);
            tmp.color = FooterColor;
            tmp.fontStyle = FontStyles.Italic;
        }

        // ── Category toggle ──────────────────────────────────────────────

        private void ToggleCategory(FeatCategory cat)
        {
            _expanded[cat] = !_expanded.TryGetValue(cat, out bool e) || !e;
            if (_rowsByCategory.TryGetValue(cat, out var rows))
                foreach (var row in rows)
                    row.SetActive(_expanded[cat]);
            if (_headerLabels.TryGetValue(cat, out var label))
                label.text = FormatHeader(cat);
        }

        // ── Formatting ───────────────────────────────────────────────────

        private string FormatHeader(FeatCategory cat)
        {
            bool isExpanded = !_expanded.TryGetValue(cat, out bool e) || e;
            string arrow = isExpanded ? "▼" : "▶";
            return $"{arrow}  {CategoryLabel(cat)}";
        }

        private static string CategoryLabel(FeatCategory cat)
        {
            switch (cat)
            {
                case FeatCategory.StonePath:   return "The Stone Path";
                case FeatCategory.VinePath:    return "The Vine Path";
                case FeatCategory.Ferment:     return "The Ferment";
                case FeatCategory.Pilgrimages: return "Pilgrimages";
                case FeatCategory.Trials:      return "Trials";
                default:                       return cat.ToString();
            }
        }

        private static int ValueFor(FeatDef def, Player player)
        {
            if (player == null) return 0;
            return def.Shape == FeatShape.CompletionistSet
                ? JournalData.GetCompletionistCount(player, def.Id)
                : JournalData.GetFeat(player, def.Id);
        }

        // Progress string per shape:
        //   TieredCounter / CompletionistSet with thresholds:
        //     "<value> / <nextThreshold>  ·  T<reached>/<max>"
        //     or "<value>  (max · <max>/<max>)" when all tiers cleared.
        //   No thresholds (UntieredRecord, or completionist not yet tiered):
        //     just "<value>".
        private static string FormatProgress(FeatDef def, Player player)
        {
            int value = ValueFor(def, player);

            if (def.Thresholds.Length == 0)
                return def.Display == DisplayFormat.GameTime
                    ? FormatGameDuration(value)
                    : value.ToString();

            int reached = 0;
            int next = -1;
            for (int i = 0; i < def.Thresholds.Length; i++)
            {
                if (value >= def.Thresholds[i])
                    reached = i + 1;
                else if (next < 0)
                    next = def.Thresholds[i];
            }

            if (next < 0)
                return $"{value}  (max · {reached}/{def.Thresholds.Length})";
            return $"{value} / {next}  ·  T{reached}/{def.Thresholds.Length}";
        }

        // Render real-seconds as humanized in-game time. Valheim runs
        // 75 real seconds per in-game hour, so 1800 real seconds = one
        // in-game day. Magnitude-appropriate: days+hours, then hours+
        // minutes, then minutes.
        private static string FormatGameDuration(int realSeconds)
        {
            double gameHours = realSeconds / 75.0;
            int days    = (int)(gameHours / 24);
            int hours   = (int)(gameHours % 24);
            int minutes = (int)((gameHours - System.Math.Floor(gameHours)) * 60);

            if (days >= 1)    return $"{Plural(days, "day")}, {Plural(hours, "hour")}";
            if (hours >= 1)   return $"{Plural(hours, "hour")}, {Plural(minutes, "minute")}";
            if (minutes >= 1) return Plural(minutes, "minute");
            return "less than a minute";
        }

        private static string Plural(int n, string unit)
            => $"{n} {unit}{(n == 1 ? "" : "s")}";
    }
}
