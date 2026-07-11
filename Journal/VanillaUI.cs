using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Borrows live vanilla UI styling (fonts, sprites, button state config) at
    // runtime so the journal matches the game's windows without shipping its
    // own assets. Lookups are lazy and cached; every path degrades gracefully
    // (null / fallback) so the panel still builds if an asset can't be found.
    //
    // We copy the *full appearance* of concrete live vanilla objects, not just
    // a bare sprite — the trophy window tints its "woodpanel_trophys" frame and
    // vanilla buttons drive hover/press through Selectable state, so borrowing
    // the sprite alone loses the shading and the highlight. So:
    //   - panel background : sprite + colour (+ material) copied from a live
    //                        Image that uses woodpanel_trophys (Trophy window)
    //   - tab/back buttons  : sprite + transition + ColorBlock + SpriteState
    //                        copied from the Craft button, so hover/press match
    //   - button labels     : font + material + colour + style of the Craft caption
    //   - title             : font + material + colour + style of the golden
    //                        Norsebold "Crafting" window header
    //   - body              : Compendium body font
    //   - scrollbar         : a clone of the Compendium vertical scrollbar
    //
    // Vanilla GUI components are reached via Resources.FindObjectsOfTypeAll
    // (returns inactive objects too) so this resolves even when those panels
    // are closed and even before their Awake has run.
    internal static class VanillaUI
    {
        private static bool _resolved;

        private static TMP_FontAsset _bodyFont;
        private static Material      _bodyMaterial;

        // Panel background (Trophy window frame).
        private static bool     _hasPanel;
        private static Sprite   _panelSprite;
        private static Color    _panelColor;
        private static Material _panelMaterial;

        // Button visuals (Craft button).
        private static bool                  _hasButton;
        private static Sprite                _buttonSprite;
        private static Selectable.Transition _btnTransition;
        private static ColorBlock            _btnColors;
        private static SpriteState           _btnSpriteState;

        // Button label (Craft caption).
        private static bool          _hasButtonLabel;
        private static TMP_FontAsset _buttonLabelFont;
        private static Material      _buttonLabelMaterial;
        private static Color         _buttonLabelColor;
        private static FontStyles    _buttonLabelStyle;

        // Title header (golden Norsebold "Crafting"/"Trophies" window header).
        private static TMP_FontAsset _titleFont;
        private static Material      _titleMaterial;
        private static Color         _titleColor;
        private static bool          _hasTitleColor;
        private static FontStyles    _titleStyle;

        // Vertical scrollbar template (Compendium scrollbar) to clone per list.
        private static Scrollbar _scrollbarTemplate;

        public static TMP_FontAsset BodyFont     { get { Resolve(); return _bodyFont; } }
        // The Compendium body's outline material — gives body text the same crisp
        // black outline the title/buttons have. Pairs with BodyFont's atlas.
        public static Material      BodyMaterial { get { Resolve(); return _bodyMaterial; } }

        // ── Panel background ─────────────────────────────────────────────────

        // Dress `dst` as the Trophy window frame (sprite + tint + material).
        // Returns false if the vanilla frame couldn't be found so the caller
        // can fall back to a flat fill.
        public static bool ApplyPanelBackground(Image dst)
        {
            Resolve();
            if (dst == null || !_hasPanel) return false;
            dst.sprite = _panelSprite;
            dst.type   = Image.Type.Sliced;
            dst.color  = _panelColor;
            if (_panelMaterial != null) dst.material = _panelMaterial;
            return true;
        }

        // ── Buttons ──────────────────────────────────────────────────────────

        // Give a journal button the vanilla button look + full state behaviour
        // (so hover / pressed match the Craft button). Falls back to a flat
        // tinted fill if the vanilla button couldn't be resolved.
        public static void StyleButton(Button btn, Image img)
        {
            Resolve();
            if (img == null) return;

            if (_hasButton)
            {
                img.sprite = _buttonSprite;
                img.type   = Image.Type.Sliced;
                img.color  = _btnColors.normalColor;
                if (btn != null)
                {
                    btn.targetGraphic = img;
                    btn.transition    = _btnTransition;
                    btn.colors        = _btnColors;
                    btn.spriteState   = _btnSpriteState;
                }
            }
            else
            {
                img.color = new Color(0.18f, 0.18f, 0.18f, 1f);
                if (btn != null) { btn.targetGraphic = img; btn.transition = Selectable.Transition.None; }
            }
        }

        // Toggle a button between its normal look and a persistent "active"
        // (selected-tab) highlight that mirrors the vanilla mouseover state.
        public static void SetButtonActive(Button btn, bool active)
        {
            Resolve();
            if (btn == null) return;
            var img = btn.targetGraphic as Image;

            switch (_hasButton ? _btnTransition : Selectable.Transition.None)
            {
                case Selectable.Transition.ColorTint:
                    var cb = btn.colors;
                    cb.normalColor = active ? _btnColors.highlightedColor : _btnColors.normalColor;
                    btn.colors = cb;
                    if (img != null) img.color = cb.normalColor; // apply immediately
                    break;

                case Selectable.Transition.SpriteSwap:
                    if (img != null)
                        img.sprite = active && _btnSpriteState.highlightedSprite != null
                            ? _btnSpriteState.highlightedSprite
                            : _buttonSprite;
                    break;

                default: // None / Animation — drive a flat tint we control
                    if (img != null)
                        img.color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                    break;
            }
        }

        public static void ApplyButtonLabelStyle(TMP_Text t)
        {
            Resolve();
            if (t == null || !_hasButtonLabel) return;
            if (_buttonLabelFont != null)     t.font               = _buttonLabelFont;
            if (_buttonLabelMaterial != null) t.fontSharedMaterial = _buttonLabelMaterial;
            t.color     = _buttonLabelColor;
            t.fontStyle = _buttonLabelStyle;
        }

        // ── Title ──────────────────────────────────────────────────────────

        public static void ApplyTitleStyle(TMP_Text t)
        {
            Resolve();
            if (t == null) return;
            if (_titleFont != null)     t.font               = _titleFont;
            if (_titleMaterial != null) t.fontSharedMaterial = _titleMaterial;
            if (_hasTitleColor)         t.color              = _titleColor;
            t.fontStyle = _titleStyle;
        }

        // ── Scrollbar ────────────────────────────────────────────────────────

        // Clone the vanilla vertical scrollbar (Compendium) into `parent`,
        // anchored to the right edge at `width`px, ready to wire to a ScrollRect.
        // Returns null if the template couldn't be found (caller scrolls without
        // a visible bar). The clone's value listeners are cleared so it only
        // drives the ScrollRect we hand it to.
        public static Scrollbar CloneScrollbar(RectTransform parent, float width)
        {
            Resolve();
            if (_scrollbarTemplate == null) return null;

            var clone = Object.Instantiate(_scrollbarTemplate.gameObject, parent);
            clone.name = "Scrollbar";
            clone.SetActive(true);

            var sb = clone.GetComponent<Scrollbar>();
            if (sb != null)
            {
                sb.onValueChanged = new Scrollbar.ScrollEvent(); // drop inherited wiring
                sb.direction      = Scrollbar.Direction.BottomToTop;
            }

            var rt = (RectTransform)clone.transform;
            rt.anchorMin        = new Vector2(1f, 0f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 0.5f);
            rt.sizeDelta        = new Vector2(width, 0f);
            rt.anchoredPosition = Vector2.zero;
            return sb;
        }

        // ── Resolution ───────────────────────────────────────────────────────

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true; // set first: a throw mid-resolve shouldn't loop

            ResolveFonts();
            ResolvePanelBackground();
            ResolveButton();
            ResolveScrollbar();

            Log.Debug(
                $"VanillaUI resolved: title={Name(_titleFont)} body={Name(_bodyFont)} " +
                $"panel={(_hasPanel ? $"{_panelSprite.name} colour={_panelColor}" : "<flat>")} " +
                $"button={(_hasButton ? $"{_buttonSprite.name} transition={_btnTransition}" : "<flat>")}");
        }

        private static void ResolveFonts()
        {
            var ig = ResolveInventoryGui();

            // Title = the golden Norsebold "Crafting" window header. The crafting
            // station-name field carries that exact style (font + outline material
            // + golden colour) and is a stable serialized donor. Fall back to the
            // recipe name, then the Compendium topic.
            if (ig != null) CaptureTitle(ig.m_craftingStationName);
            if (_titleFont == null && ig != null) CaptureTitle(ig.m_recipeName);

            var dialogs = Resources.FindObjectsOfTypeAll<TextsDialog>();
            if (dialogs.Length > 0)
            {
                var d = dialogs[0];
                if (d.m_textArea != null)
                {
                    _bodyFont     = d.m_textArea.font;
                    _bodyMaterial = d.m_textArea.fontSharedMaterial;
                }
                if (_titleFont == null) CaptureTitle(d.m_textAreaTopic);
            }

            if (_bodyFont == null) _bodyFont = FallbackBodyFont();
            if (_titleFont == null)
            {
                _titleFont     = _bodyFont;
                _titleMaterial = _bodyFont != null ? _bodyFont.material : null;
                _titleStyle    = FontStyles.Bold;
            }
        }

        private static void CaptureTitle(TMP_Text donor)
        {
            if (donor == null || donor.font == null) return;
            _titleFont     = donor.font;
            _titleMaterial = donor.fontSharedMaterial;
            _titleColor    = donor.color;
            _hasTitleColor = true;
            _titleStyle    = donor.fontStyle;
        }

        private static TMP_FontAsset FallbackBodyFont()
        {
            if (Hud.instance != null && Hud.instance.m_healthText != null && Hud.instance.m_healthText.font != null)
                return Hud.instance.m_healthText.font;

            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in loaded)
                if (f != null && f.name.Contains("Averia"))
                    return f;
            return loaded.Length > 0 ? loaded[0] : null;
        }

        // The Trophy window uses the "woodpanel_trophys" sprite but tints it.
        // Copy a live Image that uses that sprite so we inherit the colour +
        // material the trophy panel applies, not just the raw (white) sprite.
        private static void ResolvePanelBackground()
        {
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img == null || img.sprite == null) continue;
                if (!img.sprite.name.ToLowerInvariant().Contains("woodpanel_trophys")) continue;
                _panelSprite   = img.sprite;
                _panelColor    = img.color;
                _panelMaterial = img.material;
                _hasPanel      = true;
                return;
            }

            // Fallback: any woodpanel sprite, untinted.
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                if (!s.name.ToLowerInvariant().Contains("woodpanel")) continue;
                _panelSprite = s; _panelColor = Color.white; _hasPanel = true; return;
            }
        }

        // Copy the Craft button's full appearance + state behaviour.
        private static void ResolveButton()
        {
            var ig = ResolveInventoryGui();
            if (ig == null) return;
            Button donor = ig.m_craftButton != null ? ig.m_craftButton : ig.m_tabUpgrade;
            if (donor == null) return;

            var img = donor.GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                _buttonSprite   = img.sprite;
                _btnTransition  = donor.transition;
                _btnColors      = donor.colors;
                _btnSpriteState = donor.spriteState;
                _hasButton      = true;
            }

            var label = donor.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.font != null)
            {
                _buttonLabelFont     = label.font;
                _buttonLabelMaterial = label.fontSharedMaterial;
                _buttonLabelColor    = label.color;
                _buttonLabelStyle    = label.fontStyle;
                _hasButtonLabel      = true;
            }
        }

        // Compendium scrollbars are vanilla vertical Scrollbars — use one as the
        // clone template. Right (detail-pane) scrollbar preferred, then left.
        private static void ResolveScrollbar()
        {
            var dialogs = Resources.FindObjectsOfTypeAll<TextsDialog>();
            if (dialogs.Length == 0) return;
            var d = dialogs[0];
            _scrollbarTemplate = d.m_rightScrollbar != null ? d.m_rightScrollbar : d.m_leftScrollbar;
        }

        private static InventoryGui ResolveInventoryGui()
        {
            if (InventoryGui.instance != null) return InventoryGui.instance;
            var all = Resources.FindObjectsOfTypeAll<InventoryGui>();
            return all.Length > 0 ? all[0] : null;
        }

        private static string Name(TMP_FontAsset f) => f != null ? f.name : "<none>";
    }
}
