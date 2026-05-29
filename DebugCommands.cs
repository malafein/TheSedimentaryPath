#if DEBUG
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimSkills = global::Skills;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath
{
    // DEBUG-only Terminal console commands for exercising the journal and other
    // mod mechanics during development. Compiled out of Release builds entirely
    // (whole file is #if DEBUG), and registered from Plugin.Awake.
    //
    // Both commands are cheat-gated (isCheat:true), so they require `devcommands`
    // to be enabled first — which on a dedicated server means an admin. Combined
    // with #if DEBUG (absent from Release builds), they never reach players.
    //
    //   tsp_setskill <rockery|vinery|all> <level>   set a TSP skill level (0-100)
    //   tsp_setfeat  <featId> <value>               set a journal feat's value/tier
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

            Log.Info("DebugCommands: registered tsp_setskill, tsp_setfeat");
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
    }
}
#endif
