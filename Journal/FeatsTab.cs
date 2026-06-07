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
    // Each feat row shows its name, trigger description, a progress bar with
    // the value rendered over it, and the tier reached. Completionist feats
    // are additionally expandable: clicking the row reveals the specific set
    // members the player has completed (unfinished members stay hidden so the
    // journal never spoils what's left).
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

        private static readonly Color DescColor     = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color BarTrackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFillColor  = new Color(0.55f, 0.45f, 0.27f, 0.9f);
        private static readonly Color BarTextColor  = new Color(0.96f, 0.96f, 0.96f, 1f);
        private static readonly Color MemberColor   = new Color(0.82f, 0.82f, 0.82f, 1f);

        private const float CategoryHeaderHeight = 32f;
        private const float HeaderLineHeight     = 22f;
        private const float BarHeight            = 18f;
        private const float MemberLineHeight     = 20f;
        private const float TierColumnWidth      = 110f;

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

        // A feat row is a self-sizing vertical stack: header line (name + tier),
        // description, progress bar, and — for completionist feats — a
        // collapsible list of completed members. Height is driven by the
        // VerticalLayoutGroup so toggling the member list reflows the list.
        private GameObject BuildFeatRow(FeatDef def, Player player)
        {
            var row = new GameObject(
                $"Feat_{def.Id}",
                typeof(RectTransform),
                typeof(Image));
            row.transform.SetParent(ListContent, false);

            var rowImg = row.GetComponent<Image>();
            rowImg.color = FeatRowColor;

            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing                = 3;
            vlg.padding                = new RectOffset(10, 10, 6, 6);
            vlg.childAlignment         = TextAnchor.UpperLeft;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            ProgressInfo info = ComputeProgress(def, player);

            List<string> members = CollectMembers(def, player);
            bool expandable = def.Shape == FeatShape.CompletionistSet && members.Count > 0;

            // ── Header line: name (left, indented for the caret) + tier (right)
            var header = JournalUIHelpers.MakeChildRect(row.transform, "Header");
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = HeaderLineHeight;

            TextMeshProUGUI caret = null;
            if (expandable)
            {
                var caretRt = JournalUIHelpers.MakeChildRect(header, "Caret");
                caretRt.anchorMin = new Vector2(0, 0);
                caretRt.anchorMax = new Vector2(0, 1);
                caretRt.pivot     = new Vector2(0, 0.5f);
                caretRt.sizeDelta = new Vector2(16, 0);
                // Use the same large triangles as the category headers (▼/▶);
                // the small variants (▸/▾) aren't in Valheim's font and render
                // as a missing-glyph box.
                caret = JournalUIHelpers.AddText(
                    caretRt, "▶", Font, 11, TextAlignmentOptions.MidlineLeft);
                caret.color = JournalUIHelpers.AccentGold;
            }

            var nameRt = JournalUIHelpers.MakeChildRect(header, "Name");
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(expandable ? 16 : 0, 0);
            nameRt.offsetMax = new Vector2(-(TierColumnWidth + 6), 0);
            // Feat name as a bold gold header, matching the journal's title /
            // selected-row treatment.
            var nameTmp = JournalUIHelpers.AddText(
                nameRt, def.Name, Font, 15, TextAlignmentOptions.MidlineLeft);
            nameTmp.color     = JournalUIHelpers.AccentGold;
            nameTmp.fontStyle = FontStyles.Bold;

            if (!string.IsNullOrEmpty(info.Tier))
            {
                var tierRt = JournalUIHelpers.MakeChildRect(header, "Tier");
                tierRt.anchorMin = new Vector2(1, 0);
                tierRt.anchorMax = new Vector2(1, 1);
                tierRt.pivot     = new Vector2(1, 0.5f);
                tierRt.sizeDelta = new Vector2(TierColumnWidth, 0);
                // "Tier" as a yellow label, the count/state as an orange value.
                JournalUIHelpers.AddText(
                    tierRt, ColorizeTier(info.Tier), Font, 12, TextAlignmentOptions.MidlineRight);
            }

            // ── Description (the feat's trigger text) ──
            if (!string.IsNullOrEmpty(def.TriggerDescription))
            {
                var descRt = JournalUIHelpers.MakeChildRect(row.transform, "Desc");
                var desc = JournalUIHelpers.AddText(
                    descRt, def.TriggerDescription, Font, 12, TextAlignmentOptions.TopLeft);
                desc.color             = DescColor;
                desc.fontStyle         = FontStyles.Italic;
                desc.textWrappingMode  = TextWrappingModes.Normal;
            }

            // ── Progress bar with the value rendered over it ──
            BuildProgressBar(row.transform, info);

            // ── Completionist detail (collapsed by default) ──
            if (expandable)
            {
                GameObject detail = BuildMemberList(row.transform, members);
                detail.SetActive(false);

                var btn = row.AddComponent<Button>();
                btn.targetGraphic = rowImg;
                btn.transition    = Selectable.Transition.None;

                var localCaret = caret;
                btn.onClick.AddListener(() =>
                {
                    bool show = !detail.activeSelf;
                    detail.SetActive(show);
                    if (localCaret != null) localCaret.text = show ? "▼" : "▶";
                    LayoutRebuilder.ForceRebuildLayoutImmediate(ListContent);
                });
            }

            return row;
        }

        // Track + proportional fill + centered number. For untiered records
        // (no thresholds) there's no bar to fill, so the value is shown alone.
        private void BuildProgressBar(Transform parent, ProgressInfo info)
        {
            var barRt = JournalUIHelpers.MakeChildRect(parent, "Bar");
            barRt.gameObject.AddComponent<LayoutElement>().preferredHeight = BarHeight;

            if (info.HasBar)
            {
                barRt.gameObject.AddComponent<Image>().color = BarTrackColor;

                var fillRt = JournalUIHelpers.MakeChildRect(barRt, "Fill");
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(Mathf.Clamp01(info.Fill), 1f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                fillRt.gameObject.AddComponent<Image>().color = BarFillColor;
            }

            // Added last so it draws above the fill.
            var numRt = JournalUIHelpers.MakeChildRect(barRt, "Num");
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.offsetMin = new Vector2(8, 0);
            numRt.offsetMax = new Vector2(-8, 0);
            // Over a filled bar, keep the number bright for legibility against
            // the fill; a bare untiered value sits on the dark panel, so give
            // it the orange "value" colour from the palette.
            var num = JournalUIHelpers.AddText(
                numRt,
                info.HasBar ? info.Number : JournalUIHelpers.Colored(JournalUIHelpers.ValueColorTag, info.Number),
                Font,
                fontSize: 12,
                alignment: info.HasBar ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineRight);
            num.color = info.HasBar ? BarTextColor : JournalUIHelpers.BodyTextColor;
        }

        private GameObject BuildMemberList(Transform parent, List<string> members)
        {
            var detail = new GameObject("Detail", typeof(RectTransform));
            detail.transform.SetParent(parent, false);

            var dvlg = detail.AddComponent<VerticalLayoutGroup>();
            dvlg.spacing                = 1;
            dvlg.padding                = new RectOffset(18, 4, 2, 4);
            dvlg.childAlignment         = TextAnchor.UpperLeft;
            dvlg.childControlWidth      = true;
            dvlg.childControlHeight     = true;
            dvlg.childForceExpandWidth  = true;
            dvlg.childForceExpandHeight = false;

            foreach (string m in members)
            {
                var mRt = JournalUIHelpers.MakeChildRect(detail.transform, "Member");
                mRt.gameObject.AddComponent<LayoutElement>().preferredHeight = MemberLineHeight;
                var tmp = JournalUIHelpers.AddText(
                    mRt, "·  " + m, Font, 12, TextAlignmentOptions.MidlineLeft);
                tmp.color = MemberColor;
            }

            return detail;
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
            // Category as a yellow label, matching the palette used across tabs.
            return JournalUIHelpers.Colored(
                JournalUIHelpers.LabelColorTag, $"{arrow}  {CategoryLabel(cat)}");
        }

        // "Tier 2/3" → yellow "Tier" label + orange "2/3" value. The state suffix
        // (e.g. "3/3 · max") rides along in the orange value span.
        private static string ColorizeTier(string tier)
        {
            int sp = tier.IndexOf(' ');
            if (sp < 0) return JournalUIHelpers.Label(tier);
            string word = tier.Substring(0, sp);
            string rest = tier.Substring(sp + 1);
            return JournalUIHelpers.Label(word) + " "
                 + JournalUIHelpers.Colored(JournalUIHelpers.ValueColorTag, rest);
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

        // Humanized, completed-only members of a completionist set (for the
        // expandable detail). Empty for non-completionist feats.
        private static List<string> CollectMembers(FeatDef def, Player player)
        {
            var list = new List<string>();
            if (def.Shape != FeatShape.CompletionistSet || player == null)
                return list;

            foreach (string entryId in JournalData.GetCompletionistEntries(player, def.Id))
            {
                string name = CompletionistDisplay.ResolveMemberName(def.Id, entryId);
                if (!string.IsNullOrEmpty(name)) list.Add(name);
            }
            list.Sort(System.StringComparer.CurrentCultureIgnoreCase);
            return list;
        }

        // Progress rendering inputs, computed once per row.
        private struct ProgressInfo
        {
            public bool   HasBar;  // false for untiered records (no target)
            public float  Fill;    // 0..1 within the current tier segment
            public string Number;  // text drawn over the bar (or the lone value)
            public string Tier;    // "Tier 2/3" / "Tier 3/3 · max" / ""
        }

        private static ProgressInfo ComputeProgress(FeatDef def, Player player)
        {
            int value = ValueFor(def, player);
            var info = new ProgressInfo();

            // Untiered record: no threshold to fill toward — just the value.
            if (def.Thresholds.Length == 0)
            {
                info.HasBar = false;
                info.Number = FormatValue(def, value);
                info.Tier = "";
                return info;
            }

            int reached = 0;
            int next    = -1;
            for (int i = 0; i < def.Thresholds.Length; i++)
            {
                if (value >= def.Thresholds[i]) reached = i + 1;
                else if (next < 0)              next = def.Thresholds[i];
            }

            int max = def.Thresholds.Length;
            info.HasBar = true;

            if (next < 0)
            {
                // Every tier cleared.
                info.Fill   = 1f;
                info.Number = FormatValue(def, value);
                info.Tier   = $"Tier {reached}/{max} · max";
            }
            else
            {
                int segMin = reached > 0 ? def.Thresholds[reached - 1] : 0;
                info.Fill   = Mathf.Clamp01((float)(value - segMin) / (next - segMin));
                info.Number = $"{FormatValue(def, value)} / {FormatValue(def, next)}";
                info.Tier   = $"Tier {reached}/{max}";
            }

            return info;
        }

        // Format a feat value per its DisplayFormat.
        private static string FormatValue(FeatDef def, int value)
        {
            switch (def.Display)
            {
                case DisplayFormat.GameTime: return FormatGameDuration(value);
                case DisplayFormat.Distance: return FormatDistance(value);
                default:                     return value.ToString();
            }
        }

        // Whole meters → "850 m" / "1.2 km".
        private static string FormatDistance(int meters)
        {
            if (meters >= 1000)
                return $"{meters / 1000f:0.#} km";
            return $"{meters} m";
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
