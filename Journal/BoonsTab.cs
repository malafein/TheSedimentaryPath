using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Boons tab — one card per boon the player has discovered (persistent
    // tier > 0). Each card shows the boon's name + current tier, its
    // flavor description, the effects active at the current tier, and
    // the ritual instructions for refreshing. All copy comes from
    // BoonDef so adding a new boon is a TSPBoons-only change.
    //
    // Undiscovered boons are hidden per spec — the journal stays a
    // record of what you've earned, not a roadmap of what's available.
    public class BoonsTab : JournalTab
    {
        public override string Label => "Boons";

        private static readonly Color CardColor   = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        private static readonly Color EmptyColor  = new Color(0.70f, 0.70f, 0.70f, 1f);

        // Single tall row per boon. Generous height so wrapped text has
        // room; LayoutElement.preferredHeight is a hint, not a hard cap.
        private const float CardHeight = 220f;

        protected override void BuildContent(Transform parent)
            => BuildScrollableListRoot(parent, "BoonsContent");

        // Base default OnActivated clears ListContent and dispatches here.
        protected override void PopulateRows(Player player)
        {
            if (player == null) return;

            int discovered = 0;
            foreach (var def in BoonRegistry.All())
            {
                int tier = JournalData.GetBoonTier(player, def.Id);
                if (tier <= 0) continue;
                BuildBoonCard(def, tier);
                discovered++;
            }

            if (discovered == 0)
                BuildEmptyMessage();
        }

        // ── Card builder ─────────────────────────────────────────────────

        private void BuildBoonCard(BoonDef def, int currentTier)
        {
            var card = new GameObject(
                $"Boon_{def.Id}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            card.transform.SetParent(ListContent, false);
            card.GetComponent<Image>().color = CardColor;
            card.GetComponent<LayoutElement>().preferredHeight = CardHeight;

            var bodyRt = JournalUIHelpers.MakeChildRect(card.transform, "Body");
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(14, 10);
            bodyRt.offsetMax = new Vector2(-14, -10);

            var tmp = JournalUIHelpers.AddText(
                bodyRt,
                FormatCardBody(def, currentTier),
                Font,
                fontSize: 14,
                alignment: TextAlignmentOptions.TopLeft);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
        }

        private void BuildEmptyMessage()
        {
            var go = new GameObject(
                "Empty",
                typeof(RectTransform),
                typeof(LayoutElement));
            go.transform.SetParent(ListContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40;

            var tmp = JournalUIHelpers.AddText(
                (RectTransform)go.transform,
                "No boons taken.",
                Font,
                fontSize: 14,
                alignment: TextAlignmentOptions.Center);
            tmp.color = EmptyColor;
            tmp.fontStyle = FontStyles.Italic;
        }

        // ── Formatting ───────────────────────────────────────────────────

        // Renders one boon's full card body as a single rich-text string.
        // Sections: header (name + tier), description, current effects,
        // ritual. Blank lines separate sections.
        private static string FormatCardBody(BoonDef def, int currentTier)
        {
            string effects = currentTier >= 1 && currentTier <= def.EffectsByTier.Length
                ? def.EffectsByTier[currentTier - 1]
                : "(no effects)";

            var sb = new StringBuilder();
            sb.Append("<b>").Append(def.Name).Append("</b>")
              .Append("   <size=12>Tier ").Append(currentTier).Append(" / ").Append(def.MaxTier).Append("</size>")
              .AppendLine().AppendLine();

            if (!string.IsNullOrEmpty(def.Description))
                sb.AppendLine(def.Description).AppendLine();

            sb.AppendLine("<b>Current effects:</b>")
              .AppendLine(effects);

            if (!string.IsNullOrEmpty(def.RitualText))
                sb.AppendLine().Append("<b>Ritual:</b>  ").Append(def.RitualText);

            return sb.ToString();
        }
    }
}
