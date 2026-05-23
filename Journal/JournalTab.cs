using TMPro;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // One tab in the journal panel. Concrete subclasses build their own
    // content tree and assign it to Root; JournalUIController owns the
    // tab bar and toggles Root.SetActive on tab change.
    //
    // Adding a new tab: implement a subclass, add it to the list in
    // JournalUIController.BuildLayout. No other wiring needed — the
    // controller drives the tab bar from the list.
    //
    // Two common shapes are supported out of the box:
    //
    //  Simple scrollable list (FeatsTab, BoonsTab):
    //    - BuildContent calls BuildScrollableListRoot.
    //    - Override PopulateRows to fill ListContent.
    //    - Default OnActivated clears ListContent and calls PopulateRows.
    //
    //  Custom layout (LoreTab — list + detail toggle):
    //    - BuildContent builds whatever structure it needs.
    //    - Assigns ListContent itself (or skips entirely if not used).
    //    - Overrides OnActivated to drive its own activation flow.
    public abstract class JournalTab
    {
        // Title shown on the tab button.
        public abstract string Label { get; }

        // The tab's root GameObject. Assigned by BuildContent. The
        // controller activates / deactivates this to switch tabs.
        public GameObject Root { get; protected set; }

        // The font passed in at Build time. Subclasses access via this
        // property instead of carrying their own field.
        protected TMP_FontAsset Font { get; private set; }

        // Where the simple-list template's PopulateRows places rows.
        // Set by BuildScrollableListRoot, or assigned manually by tabs
        // with custom layouts that still want to use ClearContent /
        // PopulateRows.
        protected RectTransform ListContent;

        // Entry point called by the controller during panel construction.
        // Stores the font and dispatches to subclass BuildContent.
        public void Build(Transform parent, TMP_FontAsset font)
        {
            Font = font;
            BuildContent(parent);
        }

        // Subclass-specific build. Implementations must assign Root.
        // Read Font (set by Build) for any text components.
        protected abstract void BuildContent(Transform parent);

        // Convenience for tabs whose Root is the scroll list itself:
        // creates the scrollable list under `parent`, assigning Root to
        // the scroll root and ListContent to the content rect.
        protected void BuildScrollableListRoot(Transform parent, string name)
        {
            ListContent = JournalUIHelpers.BuildScrollableList(parent, name, out var root);
            Root = root;
        }

        // Called whenever this tab becomes the active one (and on Open).
        // Default behaviour: clear ListContent and call PopulateRows.
        // Tabs with a different activation flow override entirely.
        public virtual void OnActivated(Player player)
        {
            if (ListContent != null) ClearContent(ListContent);
            PopulateRows(player);
        }

        // Implement to populate the scrollable list when the tab is
        // activated. Default no-op for tabs that drive their own
        // activation in an OnActivated override.
        protected virtual void PopulateRows(Player player) { }

        // Helper for subclasses: destroy every child of `container`.
        // Reverse-iterate for the defensive idiom (Unity's Destroy is
        // deferred, so forward also works, but reverse avoids ambiguity).
        protected static void ClearContent(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
