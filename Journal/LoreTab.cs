using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Lore tab — list / detail.
    //
    // Default view is a scrollable list of unlocked entry titles
    // (LoreRegistry filtered by IsLoreUnlocked). Clicking a title swaps
    // the view to a detail pane showing the entry's title and the text
    // of the player's currently-unlocked stage. A back button returns
    // to the list.
    //
    // OnActivated rebuilds the list, so unlocks that fire while the
    // panel is closed (or while another tab is showing) appear next
    // time Lore becomes the active tab.
    public class LoreTab : JournalTab
    {
        public override string Label => "Lore";

        private GameObject       _listView;
        private GameObject       _detailView;
        private TextMeshProUGUI  _detailTitle;
        private TextMeshProUGUI  _detailBody;

        private static readonly Color ListItemColor       = new Color(0.13f, 0.13f, 0.13f, 0.6f);
        private static readonly Color BackButtonColor     = new Color(0.20f, 0.20f, 0.20f, 1f);
        private const float ListItemHeight    = 36f;
        private const float BackButtonHeight  = 28f;
        private const float DetailTitleHeight = 40f;

        protected override void BuildContent(Transform parent)
        {
            var rootRt = JournalUIHelpers.MakeChildRect(parent, "LoreContent");
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Root = rootRt.gameObject;

            BuildListView(rootRt);
            BuildDetailView(rootRt);
            ShowList();
        }

        public override void OnActivated(Player player)
        {
            ShowList();
            RebuildList(player);
        }

        // ── List view ────────────────────────────────────────────────────

        private void BuildListView(RectTransform parent)
        {
            ListContent = JournalUIHelpers.BuildScrollableList(parent, "ListView", out _listView);
        }

        private void RebuildList(Player player)
        {
            ClearContent(ListContent);
            int unlocked = 0;
            foreach (var entry in LoreRegistry.All())
            {
                if (player == null || !JournalData.IsLoreUnlocked(player, entry.Id)) continue;
                int stageIdx = JournalData.GetLoreStage(player, entry.Id);
                BuildListItem(entry, stageIdx);
                unlocked++;
            }

            if (unlocked == 0)
                BuildEmptyMessage();
        }

        private void BuildListItem(LoreEntry entry, int stageIdx)
        {
            var itemGo = new GameObject(
                $"LoreItem_{entry.Id}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            itemGo.transform.SetParent(ListContent, false);

            var img = itemGo.GetComponent<Image>();
            img.color = ListItemColor;

            var btn = itemGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => ShowDetail(entry, stageIdx));

            var layoutEl = itemGo.GetComponent<LayoutElement>();
            layoutEl.preferredHeight = ListItemHeight;

            var labelRt = JournalUIHelpers.MakeChildRect(itemGo.transform, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12, 0);
            labelRt.offsetMax = new Vector2(-8, 0);
            JournalUIHelpers.AddText(
                labelRt,
                entry.Title,
                Font,
                fontSize: 16,
                alignment: TextAlignmentOptions.MidlineLeft);
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

        // ── Detail view ──────────────────────────────────────────────────

        private void BuildDetailView(RectTransform parent)
        {
            var rt = JournalUIHelpers.MakeChildRect(parent, "DetailView");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _detailView = rt.gameObject;

            // Back button — top-left.
            var backGo = new GameObject(
                "BackButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            backGo.transform.SetParent(rt, false);
            var backRt = (RectTransform)backGo.transform;
            backRt.anchorMin        = new Vector2(0, 1);
            backRt.anchorMax        = new Vector2(0, 1);
            backRt.pivot            = new Vector2(0, 1);
            backRt.sizeDelta        = new Vector2(90, BackButtonHeight);
            backRt.anchoredPosition = Vector2.zero;

            var backImg = backGo.GetComponent<Image>();
            backImg.color = BackButtonColor;
            var backBtn = backGo.GetComponent<Button>();
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(ShowList);

            var backLabelRt = JournalUIHelpers.MakeChildRect(backRt, "Label");
            backLabelRt.anchorMin = Vector2.zero;
            backLabelRt.anchorMax = Vector2.one;
            backLabelRt.offsetMin = Vector2.zero;
            backLabelRt.offsetMax = Vector2.zero;
            JournalUIHelpers.AddText(
                backLabelRt,
                "Back",
                Font,
                fontSize: 14,
                alignment: TextAlignmentOptions.Center);

            // Title — centered, below the back button.
            var titleRt = JournalUIHelpers.MakeChildRect(rt, "Title");
            titleRt.anchorMin        = new Vector2(0, 1);
            titleRt.anchorMax        = new Vector2(1, 1);
            titleRt.pivot            = new Vector2(0.5f, 1);
            titleRt.sizeDelta        = new Vector2(0, DetailTitleHeight);
            titleRt.anchoredPosition = new Vector2(0, -(BackButtonHeight + 4));
            _detailTitle = JournalUIHelpers.AddText(
                titleRt,
                "",
                Font,
                fontSize: 22,
                alignment: TextAlignmentOptions.Center);

            // Body — fills remaining area below the title.
            float bodyTopOffset = BackButtonHeight + 4 + DetailTitleHeight + 8;
            var bodyRt = JournalUIHelpers.MakeChildRect(rt, "Body");
            bodyRt.anchorMin = new Vector2(0, 0);
            bodyRt.anchorMax = new Vector2(1, 1);
            bodyRt.offsetMin = new Vector2(20, 20);
            bodyRt.offsetMax = new Vector2(-20, -bodyTopOffset);
            _detailBody = JournalUIHelpers.AddText(
                bodyRt,
                "",
                Font,
                fontSize: 16,
                alignment: TextAlignmentOptions.TopLeft);
            _detailBody.textWrappingMode = TextWrappingModes.Normal;
        }

        private void ShowList()
        {
            if (_listView != null)   _listView.SetActive(true);
            if (_detailView != null) _detailView.SetActive(false);
        }

        private void ShowDetail(LoreEntry entry, int stageIdx)
        {
            if (entry == null || entry.Stages.Count == 0) return;
            // Clamp in case stored stage is somehow out of bounds.
            stageIdx = Mathf.Clamp(stageIdx, 0, entry.Stages.Count - 1);

            _detailTitle.text = entry.Title;
            _detailBody.text  = entry.Stages[stageIdx].Text;

            if (_listView != null)   _listView.SetActive(false);
            if (_detailView != null) _detailView.SetActive(true);
        }
    }
}
