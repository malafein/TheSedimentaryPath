using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Lore tab — master / detail (Compendium-style).
    //
    // Left pane: a scrollable list of unlocked entry titles (LoreRegistry
    // filtered by IsLoreUnlocked). Right pane: the selected entry's title
    // and the text of the player's currently-unlocked stage. Both panes are
    // visible at once; selecting a list row updates the detail in place (no
    // back button). The selected row is highlighted gold like the vanilla
    // Compendium.
    //
    // OnActivated rebuilds the list, so unlocks that fire while the panel is
    // closed (or while another tab is showing) appear next time Lore becomes
    // the active tab. Selection is preserved across rebuilds by entry id;
    // otherwise the first entry is selected.
    public class LoreTab : JournalTab
    {
        public override string Label => "Lore";

        private GameObject       _detailBodyScroll;
        private TextMeshProUGUI  _detailTitle;
        private TextMeshProUGUI  _detailBody;

        // Selection state. Tracked by id so it survives list rebuilds.
        private string           _selectedId;
        private Image            _selectedBg;
        private TextMeshProUGUI  _selectedLabel;
        private readonly List<ItemView> _items = new List<ItemView>();

        private const float LeftPaneFraction = 0.34f;
        private const float PaneGap          = 10f;
        private const float ListItemHeight   = 36f;
        private const float DetailTitleHeight = 40f;

        private static readonly Color NormalItemColor   = new Color(0f, 0f, 0f, 0f);
        private static readonly Color SelectedItemColor = new Color(1f, 0.718f, 0.36f, 0.22f);

        // One list row's clickable handle + the bits SelectEntry recolors.
        private class ItemView
        {
            public LoreEntry        Entry;
            public int              StageIdx;
            public Image            Bg;
            public TextMeshProUGUI  Label;
        }

        // ── Build ────────────────────────────────────────────────────────

        protected override void BuildContent(Transform parent)
        {
            var rootRt = JournalUIHelpers.MakeChildRect(parent, "LoreContent");
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Root = rootRt.gameObject;

            // Left: entry list.
            var listPaneRt = JournalUIHelpers.MakeChildRect(rootRt, "ListPane");
            listPaneRt.anchorMin = new Vector2(0, 0);
            listPaneRt.anchorMax = new Vector2(LeftPaneFraction, 1);
            listPaneRt.offsetMin = Vector2.zero;
            listPaneRt.offsetMax = Vector2.zero;
            ListContent = JournalUIHelpers.BuildScrollableList(listPaneRt, "ListView", out _);

            // Thin divider between the panes.
            var divRt = JournalUIHelpers.MakeChildRect(rootRt, "Divider");
            divRt.anchorMin        = new Vector2(LeftPaneFraction, 0);
            divRt.anchorMax        = new Vector2(LeftPaneFraction, 1);
            divRt.pivot            = new Vector2(0.5f, 0.5f);
            divRt.sizeDelta        = new Vector2(2, -16);
            divRt.anchoredPosition = new Vector2(PaneGap * 0.5f, 0);
            var divImg = divRt.gameObject.AddComponent<Image>();
            divImg.color         = new Color(1f, 1f, 1f, 0.12f);
            divImg.raycastTarget = false;

            // Right: selected entry detail.
            var detailPaneRt = JournalUIHelpers.MakeChildRect(rootRt, "DetailPane");
            detailPaneRt.anchorMin = new Vector2(LeftPaneFraction, 0);
            detailPaneRt.anchorMax = new Vector2(1, 1);
            detailPaneRt.offsetMin = new Vector2(PaneGap, 0);
            detailPaneRt.offsetMax = Vector2.zero;
            BuildDetailPane(detailPaneRt);
        }

        private void BuildDetailPane(RectTransform parent)
        {
            // Topic title — fixed at the top, gold like the Compendium topic.
            var titleRt = JournalUIHelpers.MakeChildRect(parent, "DetailTitle");
            titleRt.anchorMin        = new Vector2(0, 1);
            titleRt.anchorMax        = new Vector2(1, 1);
            titleRt.pivot            = new Vector2(0.5f, 1);
            titleRt.sizeDelta        = new Vector2(0, DetailTitleHeight);
            titleRt.anchoredPosition = new Vector2(0, -4);
            _detailTitle = JournalUIHelpers.AddText(
                titleRt,
                "",
                Font,
                fontSize: 22,
                alignment: TextAlignmentOptions.TopLeft);
            _detailTitle.fontStyle = FontStyles.Bold;
            _detailTitle.color     = JournalUIHelpers.AccentGold;

            // Body — scrollable, fills the area under the title.
            var bodyPaneRt = JournalUIHelpers.MakeChildRect(parent, "DetailBodyPane");
            bodyPaneRt.anchorMin = new Vector2(0, 0);
            bodyPaneRt.anchorMax = new Vector2(1, 1);
            bodyPaneRt.offsetMin = Vector2.zero;
            bodyPaneRt.offsetMax = new Vector2(0, -(DetailTitleHeight + 8));

            var bodyContent = JournalUIHelpers.BuildScrollableList(bodyPaneRt, "DetailBody", out _detailBodyScroll);
            var bodyRowRt = JournalUIHelpers.MakeChildRect(bodyContent, "Text");
            _detailBody = JournalUIHelpers.AddText(
                bodyRowRt,
                "",
                Font,
                fontSize: 16,
                alignment: TextAlignmentOptions.TopLeft);
            _detailBody.textWrappingMode = TextWrappingModes.Normal;
        }

        // ── Activation / list ──────────────────────────────────────────────

        public override void OnActivated(Player player) => RebuildList(player);

        private void RebuildList(Player player)
        {
            ClearContent(ListContent);
            _items.Clear();
            _selectedBg    = null; // destroyed by ClearContent
            _selectedLabel = null;

            foreach (var entry in LoreRegistry.All())
            {
                if (player == null || !JournalData.IsLoreUnlocked(player, entry.Id)) continue;
                int stageIdx = JournalData.GetLoreStage(player, entry.Id);
                _items.Add(BuildListItem(entry, stageIdx));
            }

            if (_items.Count == 0)
            {
                _selectedId = null;
                BuildEmptyMessage();
                ShowDetail(null, 0);
                return;
            }

            // Reselect the previously-shown entry if it's still present.
            ItemView target = _selectedId != null
                ? _items.Find(v => v.Entry.Id == _selectedId)
                : null;
            SelectEntry(target ?? _items[0]);
        }

        private ItemView BuildListItem(LoreEntry entry, int stageIdx)
        {
            var itemGo = new GameObject(
                $"LoreItem_{entry.Id}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            itemGo.transform.SetParent(ListContent, false);

            var img = itemGo.GetComponent<Image>();
            img.color = NormalItemColor;

            var btn = itemGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.None;

            itemGo.GetComponent<LayoutElement>().preferredHeight = ListItemHeight;

            var labelRt = JournalUIHelpers.MakeChildRect(itemGo.transform, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12, 0);
            labelRt.offsetMax = new Vector2(-8, 0);
            var label = JournalUIHelpers.AddText(
                labelRt,
                entry.Title,
                Font,
                fontSize: 16,
                alignment: TextAlignmentOptions.MidlineLeft);

            var view = new ItemView { Entry = entry, StageIdx = stageIdx, Bg = img, Label = label };
            btn.onClick.AddListener(() => SelectEntry(view));
            return view;
        }

        private void BuildEmptyMessage()
        {
            var emptyGo = new GameObject(
                "Empty",
                typeof(RectTransform),
                typeof(LayoutElement));
            emptyGo.transform.SetParent(ListContent, false);
            emptyGo.GetComponent<LayoutElement>().preferredHeight = 40;
            JournalUIHelpers.AddText(
                (RectTransform)emptyGo.transform,
                "Nothing recorded yet.",
                Font,
                fontSize: 14,
                alignment: TextAlignmentOptions.Center);
        }

        // ── Selection / detail ──────────────────────────────────────────────

        private void SelectEntry(ItemView view)
        {
            if (view == null) return;
            _selectedId = view.Entry.Id;

            if (_selectedBg != null)    _selectedBg.color    = NormalItemColor;
            if (_selectedLabel != null) _selectedLabel.color = JournalUIHelpers.BodyTextColor;

            _selectedBg    = view.Bg;
            _selectedLabel = view.Label;
            if (_selectedBg != null)    _selectedBg.color    = SelectedItemColor;
            if (_selectedLabel != null) _selectedLabel.color = JournalUIHelpers.AccentGold;

            ShowDetail(view.Entry, view.StageIdx);
        }

        // Divider drawn between accreted (Append) stages — a thin centred rule.
        private const string StageDivider = "\n\n<align=\"center\"><color=#9b8b6a>─── · ───</color></align>\n\n";

        private void ShowDetail(LoreEntry entry, int stageIdx)
        {
            if (entry == null || entry.Stages.Count == 0)
            {
                _detailTitle.text = "";
                _detailBody.text  = "";
                return;
            }
            // Clamp in case stored stage is somehow out of bounds.
            stageIdx = Mathf.Clamp(stageIdx, 0, entry.Stages.Count - 1);
            _detailTitle.text = entry.Title;
            _detailBody.text  = BuildBody(entry, stageIdx);
        }

        // Build the visible body by walking every unlocked stage (0..stageIdx) in
        // order: an Append stage accretes below the prior text with a divider; a
        // Replace stage (the default) supersedes everything before it.
        private static string BuildBody(LoreEntry entry, int stageIdx)
        {
            string body = "";
            for (int i = 0; i <= stageIdx; i++)
            {
                LoreStage stage = entry.Stages[i];
                if (body.Length == 0 || stage.Mode == LoreStageMode.Replace)
                    body = stage.Text;
                else
                    body += StageDivider + stage.Text;
            }
            return body;
        }
    }
}
