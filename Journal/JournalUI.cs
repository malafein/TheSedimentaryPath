using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Top-level controller for the in-game journal panel. Display name
    // is still TBD (see dev/journal.md).
    //
    // The MonoBehaviour itself lives below as JournalUIController. The
    // static surface here is what hotkey patches call. JournalUIController
    // binds itself in its Build() so calls before HudAwakePatch fires
    // (shouldn't normally happen) no-op safely.
    public static class JournalUI
    {
        private static JournalUIController _controller;

        public static bool IsOpen => _controller != null && _controller.IsOpen;

        internal static void Bind(JournalUIController controller) => _controller = controller;

        public static void Open()
        {
            if (_controller == null)
            {
                Log.Warn("JournalUI.Open: no controller bound (HudAwakePatch may not have fired yet)");
                return;
            }
            _controller.Open();
        }

        public static void Close() => _controller?.Close();
    }

    // The panel GameObject's own controller — the panel and the
    // MonoBehaviour are the same GameObject so that closing (SetActive
    // false) naturally stops the Update loop (which only needs to run
    // while the panel is open to listen for close keys).
    //
    // Layout: Title bar at top, tab bar below it, content area fills the
    // remainder. Each tab is a JournalTab subclass that builds its own
    // content tree under the content area; the controller drives the
    // tab bar buttons and the SwitchTab dispatch from a list of tabs.
    // Add a tab by adding a class to the list in BuildLayout — no other
    // wiring needed.
    public class JournalUIController : MonoBehaviour
    {
        public bool IsOpen => gameObject.activeSelf;

        private List<JournalTab> _tabs;
        private Image[]          _tabButtonBgs;
        private int              _activeTabIndex;

        private static readonly Color TabActiveColor   = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color TabInactiveColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        private const float TitleHeight   = 60f;
        private const float TabBarHeight  = 50f;
        private const float ContentMargin = 20f;

        // ── Build ────────────────────────────────────────────────────────

        // Builds the panel hierarchy as a child of `parent` and returns
        // the JournalUIController on it. Panel starts hidden.
        public static JournalUIController Build(Transform parent)
        {
            TMP_FontAsset font = ResolveFont();

            var go = new GameObject(
                "TSP_JournalPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(JournalUIController));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(800, 600);
            rt.anchoredPosition = Vector2.zero;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

            var controller = go.GetComponent<JournalUIController>();
            controller.BuildLayout(font);

            go.SetActive(false);
            JournalUI.Bind(controller);
            Log.Info($"JournalUIController: built (font={(font != null ? font.name : "<none>")})");
            return controller;
        }

        private void BuildLayout(TMP_FontAsset font)
        {
            // Title bar across the top.
            var titleRt = JournalUIHelpers.MakeChildRect(transform, "Title");
            titleRt.anchorMin        = new Vector2(0, 1);
            titleRt.anchorMax        = new Vector2(1, 1);
            titleRt.pivot            = new Vector2(0.5f, 1);
            titleRt.sizeDelta        = new Vector2(0, TitleHeight);
            titleRt.anchoredPosition = new Vector2(0, -10);
            JournalUIHelpers.AddText(
                titleRt,
                "Journal",
                font,
                fontSize: 32,
                alignment: TextAlignmentOptions.Center);

            // Tabs catalog — order here is left-to-right in the tab bar.
            _tabs = new List<JournalTab>
            {
                new LoreTab(),
                new FeatsTab(),
                new BoonsTab(),
            };

            // Tab bar — N equal-width buttons below the title.
            var tabBarRt = JournalUIHelpers.MakeChildRect(transform, "TabBar");
            tabBarRt.anchorMin        = new Vector2(0, 1);
            tabBarRt.anchorMax        = new Vector2(1, 1);
            tabBarRt.pivot            = new Vector2(0.5f, 1);
            tabBarRt.sizeDelta        = new Vector2(0, TabBarHeight);
            tabBarRt.anchoredPosition = new Vector2(0, -(TitleHeight + 10));

            _tabButtonBgs = new Image[_tabs.Count];
            for (int i = 0; i < _tabs.Count; i++)
            {
                int captured = i; // closure capture for click handler
                _tabButtonBgs[i] = BuildTabButton(
                    parent: tabBarRt,
                    label: _tabs[i].Label,
                    index: i,
                    tabCount: _tabs.Count,
                    font: font,
                    onClick: () => SwitchTab(captured));
            }

            // Content area — fills the space under the tab bar with margins.
            float contentTopOffset = TitleHeight + TabBarHeight + 20f;
            var contentAreaRt = JournalUIHelpers.MakeChildRect(transform, "ContentArea");
            contentAreaRt.anchorMin = new Vector2(0, 0);
            contentAreaRt.anchorMax = new Vector2(1, 1);
            contentAreaRt.offsetMin = new Vector2(ContentMargin, ContentMargin);
            contentAreaRt.offsetMax = new Vector2(-ContentMargin, -contentTopOffset);

            // Each tab builds its content under the shared content area.
            foreach (var tab in _tabs)
                tab.Build(contentAreaRt, font);

            SwitchTab(0);
        }

        // ── Tab switching ────────────────────────────────────────────────

        private void SwitchTab(int index)
        {
            if (_tabs == null || index < 0 || index >= _tabs.Count) return;
            _activeTabIndex = index;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Root?.SetActive(i == index);
                _tabButtonBgs[i].color = (i == index) ? TabActiveColor : TabInactiveColor;
            }
            _tabs[index].OnActivated(Player.m_localPlayer);
        }

        // ── Open / Close / Update ────────────────────────────────────────

        public void Open()
        {
            gameObject.SetActive(true);
            // Refresh the active tab — content may have changed while
            // the panel was closed (e.g. lore unlocked between sessions).
            if (_tabs != null && _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
                _tabs[_activeTabIndex].OnActivated(Player.m_localPlayer);
            Log.Debug("JournalUI: opened");
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Log.Debug("JournalUI: closed");
        }

        private void Update()
        {
            // Update only runs while the panel is active. ESC and the
            // toggle hotkey both close.
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            var shortcut = Plugin.JournalHotkey.Value;
            if (shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey))
                Close();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        // Clone a TMP font asset off an existing vanilla TMP_Text — most
        // robust against asset renames. Falls back to the first Averia*
        // asset, then the first loaded TMP_FontAsset of any kind, then null.
        private static TMP_FontAsset ResolveFont()
        {
            if (Hud.instance != null && Hud.instance.m_healthText != null)
            {
                var f = Hud.instance.m_healthText.font;
                if (f != null) return f;
            }
            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in loaded)
                if (f != null && f.name.Contains("Averia"))
                    return f;
            return loaded.Length > 0 ? loaded[0] : null;
        }

        // Build a tab button in the tab bar. `index` is 0..tabCount-1; we
        // lay the buttons out evenly across the bar width using anchor
        // stretch. Returns the background Image so SwitchTab can recolor.
        private static Image BuildTabButton(
            RectTransform parent,
            string label,
            int index,
            int tabCount,
            TMP_FontAsset font,
            UnityAction onClick)
        {
            var go = new GameObject(
                $"Tab_{label}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            float widthFraction = 1f / tabCount;
            rt.anchorMin = new Vector2(widthFraction * index, 0);
            rt.anchorMax = new Vector2(widthFraction * (index + 1), 1);
            rt.offsetMin = new Vector2(10, 5);
            rt.offsetMax = new Vector2(-10, -5);

            var img = go.GetComponent<Image>();
            img.color = TabInactiveColor;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Label fills the button.
            var labelRt = JournalUIHelpers.MakeChildRect(go.transform, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            JournalUIHelpers.AddText(
                labelRt,
                label,
                font,
                fontSize: 18,
                alignment: TextAlignmentOptions.Center);

            return img;
        }
    }
}
