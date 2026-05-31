#if DEBUG
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath
{
    // DEBUG-only Terminal console commands for exercising the journal and other
    // mod mechanics during development. Compiled out of Release builds entirely
    // (whole file is #if DEBUG), and registered from Plugin.Awake.
    //
    // The state-mutating commands are cheat-gated (isCheat:true), so they
    // require `devcommands` first — which on a dedicated server means an admin.
    // The read-only dump command is not gated. Either way #if DEBUG (absent
    // from Release builds) keeps them all away from players.
    //
    //   tsp_setskill <rockery|vinery|all> <level>   set a TSP skill level (0-100)
    //   tsp_setfeat  <featId> <value>               set a journal feat's value/tier
    //   tsp_dumpui                                   log vanilla UI donor details
    //
    // tsp_setskill replaces the old DebugSkillSet25/50 hotkeys.
    internal static class DebugCommands
    {
        // Player.m_skills + Skills.GetSkill are reached by reflection (same
        // approach the old HotkeyInputPatch debug block used).
        private static readonly FieldInfo _skillsField =
            AccessTools.Field(typeof(Player), "m_skills");
        private static readonly MethodInfo _getSkillMethod =
            AccessTools.Method(typeof(ValheimSkills), "GetSkill", new[] { typeof(ValheimSkills.SkillType) });

        public static void Register()
        {
            new Terminal.ConsoleCommand("tsp_setskill",
                "TSP debug: set a TSP skill level. Usage: tsp_setskill <rockery|vinery|all> <level 0-100>",
                SetSkill,
                isCheat: true,
                optionsFetcher: () => new List<string> { "rockery", "vinery", "all" });

            new Terminal.ConsoleCommand("tsp_setfeat",
                "TSP debug: set a journal feat's value. Usage: tsp_setfeat <featId> <value>",
                SetFeat,
                isCheat: true,
                optionsFetcher: () => FeatRegistry.All().Select(d => d.Id).OrderBy(id => id).ToList());

            // Read-only diagnostic — logs UI donor details, can't affect
            // gameplay, so it is NOT cheat-gated (devcommands not required).
            new Terminal.ConsoleCommand("tsp_dumpui",
                "TSP debug: log vanilla UI donor details (trophy frame + Craft button state) to the player log.",
                DumpUI);

            Log.Info("DebugCommands: registered tsp_setskill, tsp_setfeat, tsp_dumpui");
        }

        private static void SetSkill(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null) { args.Context.AddString("tsp_setskill: no local player"); return; }

            if (args.Length < 2)
            {
                args.Context.AddString("Usage: tsp_setskill <rockery|vinery|all> <level 0-100>");
                return;
            }

            string which = args[1].ToLowerInvariant();
            if (!args.TryParameterInt(2, out int level))
            {
                args.Context.AddString("tsp_setskill: needs a numeric level, e.g. tsp_setskill rockery 50");
                return;
            }
            level = Mathf.Clamp(level, 0, 100);

            bool any = false;
            if (which == "rockery" || which == "all")
            {
                ApplySkill(player, RockerySkill.SkillType, level);
                args.Context.AddString($"Rockery set to {level}");
                any = true;
            }
            if (which == "vinery" || which == "all")
            {
                ApplySkill(player, VinerySkill.SkillType, level);
                args.Context.AddString($"Vinery set to {level}");
                any = true;
            }
            if (!any)
                args.Context.AddString($"tsp_setskill: unknown skill '{args[1]}' (use rockery, vinery, or all)");
        }

        private static void ApplySkill(Player player, ValheimSkills.SkillType type, float level)
        {
            var skills = (ValheimSkills)_skillsField?.GetValue(player);
            if (skills == null) return;
            var skill = (ValheimSkills.Skill)_getSkillMethod?.Invoke(skills, new object[] { type });
            if (skill == null) return;
            skill.m_level = level;
            skill.m_accumulator = 0f;
            Log.Debug($"DebugCommands: set {type} to {level}");
        }

        private static void SetFeat(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null) { args.Context.AddString("tsp_setfeat: no local player"); return; }

            if (args.Length < 2)
            {
                args.Context.AddString("Usage: tsp_setfeat <featId> <value>   (tab-complete the id)");
                return;
            }

            string featId = args[1];
            FeatDef def = FeatRegistry.Get(featId);
            if (def == null)
            {
                args.Context.AddString($"tsp_setfeat: unknown feat '{featId}'");
                return;
            }
            if (!args.TryParameterInt(2, out int value) || value < 0)
            {
                args.Context.AddString("tsp_setfeat: needs a non-negative integer value");
                return;
            }

            if (def.Shape == FeatShape.CompletionistSet)
            {
                // Completionist feats are set-backed (tier reads the distinct
                // count), so "setting" them means filling the set. Additive
                // only — we never delete real entries, so a lower target is a
                // no-op rather than a silent data loss.
                //
                // TODO: support adding real entries (actual boss / biome /
                // runestone / trader IDs) instead of fabricated tsp_dbg_<n>
                // placeholders — e.g. a companion `tsp_addfeat <featId> <entryId>`
                // command — so completionist set views reflect meaningful data.
                int current = JournalData.GetCompletionistCount(player, featId);
                if (value <= current)
                {
                    args.Context.AddString(
                        $"tsp_setfeat: '{featId}' is a completionist set already at {current}; can't reduce to {value}");
                    return;
                }
                for (int i = current; i < value; i++)
                    JournalData.AddCompletionistEntry(player, featId, $"tsp_dbg_{i}");
                LoreChecker.NotifyFeatChanged(player, featId);
                int tier = FeatTracker.GetCurrentTier(player, featId);
                args.Context.AddString(
                    $"{featId}: count {current} -> {value}  (T{tier}/{def.Thresholds.Length}, dummy entries added)");
            }
            else
            {
                JournalData.SetFeat(player, featId, value);
                LoreChecker.NotifyFeatChanged(player, featId);
                int tier = FeatTracker.GetCurrentTier(player, featId);
                string tierStr = def.Thresholds.Length > 0 ? $"  (T{tier}/{def.Thresholds.Length})" : "";
                args.Context.AddString($"{featId} = {value}{tierStr}");
            }
        }

        // Logs the vanilla UI donors the journal borrows from, so styling work
        // can match them exactly instead of guessing: the Trophy-window frame
        // Image and the Craft button's full Selectable state config (including
        // any child "glow" object that might drive its hover highlight).
        private static void DumpUI(Terminal.ConsoleEventArgs args)
        {
            Log.Info("── tsp_dumpui ───────────────────────────────────────────");

            // Trophy frame: every Image using a woodpanel sprite.
            int wood = 0;
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img == null || img.sprite == null) continue;
                if (!img.sprite.name.ToLowerInvariant().Contains("woodpanel")) continue;
                wood++;
                Log.Info($"  woodpanel Image: path={Path(img.transform)} sprite={img.sprite.name} " +
                         $"colour={img.color} type={img.type} mat={(img.material != null ? img.material.name : "<none>")} " +
                         $"border={img.sprite.border}");
            }
            if (wood == 0) Log.Info("  (no woodpanel Images found)");

            // Craft button: full Selectable config + children (to spot a glow).
            var ig = InventoryGui.instance;
            if (ig == null)
            {
                var all = Resources.FindObjectsOfTypeAll<InventoryGui>();
                if (all.Length > 0) ig = all[0];
            }
            Button btn = ig != null ? ig.m_craftButton : null;
            if (btn == null) { Log.Info("  (no Craft button found)"); }
            else
            {
                var bimg = btn.GetComponent<Image>();
                var cb   = btn.colors;
                var ss   = btn.spriteState;
                Log.Info($"  CraftButton: transition={btn.transition} sprite={(bimg != null && bimg.sprite != null ? bimg.sprite.name : "<none>")} " +
                         $"imgColour={(bimg != null ? bimg.color.ToString() : "<none>")}");
                Log.Info($"    colours: normal={cb.normalColor} highlighted={cb.highlightedColor} pressed={cb.pressedColor} " +
                         $"selected={cb.selectedColor} disabled={cb.disabledColor} mult={cb.colorMultiplier} fade={cb.fadeDuration}");
                Log.Info($"    spriteState: highlighted={Sn(ss.highlightedSprite)} pressed={Sn(ss.pressedSprite)} " +
                         $"selected={Sn(ss.selectedSprite)} disabled={Sn(ss.disabledSprite)}");
                foreach (Transform child in btn.transform)
                {
                    var ci = child.GetComponent<Image>();
                    var ct = child.GetComponent<TMP_Text>();
                    Log.Info($"    child '{child.name}' active={child.gameObject.activeSelf} " +
                             $"img={(ci != null && ci.sprite != null ? ci.sprite.name : "-")} " +
                             $"text={(ct != null ? $"\"{ct.text}\" colour={ct.color} mat={(ct.fontSharedMaterial != null ? ct.fontSharedMaterial.name : "-")}" : "-")}");
                }
            }

            // Title / header donor candidates — for matching the journal title
            // to the golden all-caps "CRAFTING" / "TROPHIES" window headers and
            // the golden trophy-name style (future feat-category headers).
            Log.Info("  -- header / title candidates --");
            if (ig != null)
            {
                DumpText("InventoryGui.m_recipeName", ig.m_recipeName);
                DumpText("InventoryGui.m_craftingStationName", ig.m_craftingStationName);
            }
            var tds = Resources.FindObjectsOfTypeAll<TextsDialog>();
            if (tds.Length > 0) DumpText("TextsDialog.m_textAreaTopic", tds[0].m_textAreaTopic);

            // Scan all TMP texts for short header-like captions and dump their style.
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || string.IsNullOrEmpty(t.text)) continue;
                string txt = t.text.Trim();
                if (txt.Length == 0 || txt.Length > 16) continue;
                string low = txt.ToLowerInvariant();
                if (low.Contains("craft") || low.Contains("troph") || low.Contains("compend"))
                    DumpText($"scan[{Path(t.transform)}]", t);
            }

            Log.Info("─────────────────────────────────────────────────────────");
            args.Context.AddString("tsp_dumpui: wrote vanilla UI donor details to the player log.");
        }

        private static void DumpText(string label, TMP_Text t)
        {
            if (t == null) { Log.Info($"    {label}: <null>"); return; }
            Log.Info($"    {label}: text=\"{t.text}\" font={(t.font != null ? t.font.name : "-")} " +
                     $"mat={(t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "-")} colour={t.color} " +
                     $"style={t.fontStyle} size={t.fontSize}");
        }

        private static string Sn(Sprite s) => s != null ? s.name : "<none>";

        private static string Path(Transform t)
        {
            string p = t.name;
            for (Transform cur = t.parent; cur != null; cur = cur.parent)
                p = cur.name + "/" + p;
            return p;
        }
    }
}
#endif
