using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Attach RockShrineComponent to every placed Mysterious Rock so each instance
    // manages its own ZDO-timestamp-based shrine timer (beehive/plant pattern).
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class RockPieceAwakePatch
    {
        public static void Postfix(Piece __instance)
        {
            if (Utils.GetPrefabName(__instance.gameObject) != RockShrine.RockPrefabName)
                return;
            if (__instance.GetComponent<RockShrineComponent>() == null)
                __instance.gameObject.AddComponent<RockShrineComponent>();
        }
    }

    // Award 25 XP the first time a player places a Mysterious Rock
    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    public static class PlaceMysteriousRockPatch
    {
        private const string UniqueKey = "TheSedimentaryPath_PlacedMysteriousRock";

        public static void Postfix(Player __instance, Piece piece, bool __result)
        {
            if (!__result || piece == null)
                return;

            if (Utils.GetPrefabName(piece.gameObject) != "Placeable_HardRock")
                return;

            if (__instance.HaveUniqueKey(UniqueKey))
            {
                ZLog.Log("[TheSedimentaryPath] PlaceMysteriousRock: already placed before, skipping XP");
                return;
            }

            __instance.AddUniqueKey(UniqueKey);
            __instance.RaiseSkill(RockerySkill.SkillType, 25f);
            ZLog.Log("[TheSedimentaryPath] PlaceMysteriousRock: first placement! Awarded 25 XP");
        }
    }

    // Award 0.25 XP when petting the Mysterious Rock
    [HarmonyPatch(typeof(Pet), nameof(Pet.Interact))]
    public static class PetMysteriousRockPatch
    {
        public static void Postfix(Pet __instance, Humanoid user, bool hold, bool __result)
        {
            if (!__result || hold)
                return;

            Player player = user as Player;
            if (player == null)
                return;

            player.RaiseSkill(RockerySkill.SkillType, 0.25f);
            ZLog.Log("[TheSedimentaryPath] PetMysteriousRock: petted the rock, awarded 0.25 XP");
        }
    }

    // Award 25 XP when the player hears the Mysterious Rock speak
    [HarmonyPatch(typeof(Chat), nameof(Chat.SetNpcText))]
    public static class MysteriousRockSpeakPatch
    {
        public static void Postfix(GameObject talker)
        {
            if (talker == null)
                return;

            if (Utils.GetPrefabName(talker) != "Placeable_HardRock")
                return;

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            player.RaiseSkill(RockerySkill.SkillType, 25f);
            ZLog.Log("[TheSedimentaryPath] MysteriousRockSpeak: the Rock has spoken! Awarded 25 XP");
        }
    }

    // Debug hover text: shrine score and next-check countdown
    [HarmonyPatch(typeof(Pet), nameof(Pet.GetHoverText))]
    public static class RockShrineHoverPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pet __instance, ref string __result)
        {
            if (!Plugin.DebugMode.Value) return;
            if (Utils.GetPrefabName(__instance.gameObject) != RockShrine.RockPrefabName) return;

            int score = RockShrine.ComputeScore(__instance.transform.position, __instance.gameObject);
            float t      = Mathf.Clamp01((float)(score - RockShrine.MinScore) / (RockShrine.MaxScoreCap - RockShrine.MinScore));
            float chance = Mathf.Lerp(RockShrine.MinChance, RockShrine.MaxChance, t);

            string scoreColor = score >= RockShrine.MinScore ? "#0F0" : "#F80";
            __result += $"<size=12>\n<color={scoreColor}>[Shrine] score={score} | chance={chance:P0}</color>";

            var nview = __instance.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && ZNet.instance != null)
            {
                long lastCheckTicks = nview.GetZDO().GetLong("TSP_ShrineLastCheck", 0L);
                if (lastCheckTicks == 0L)
                {
                    __result += " | next: pending";
                }
                else
                {
                    float baseInterval = Plugin.ShrineIntervalDebug != null && Plugin.ShrineIntervalDebug.Value > 0f
                        ? Plugin.ShrineIntervalDebug.Value : RockShrine.DefaultInterval;
                    System.DateTime lastCheck = new System.DateTime(lastCheckTicks);
                    float elapsed   = (float)(ZNet.instance.GetTime() - lastCheck).TotalSeconds;
                    float remaining = baseInterval - elapsed;
                    if (remaining <= 0f)
                        __result += " | <color=#0FF>ready</color>";
                    else
                        __result += $" | next: {remaining:F0}s";
                }
            }

            __result += "</size>";
        }
    }
}
