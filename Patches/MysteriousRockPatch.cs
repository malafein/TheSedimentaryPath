using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
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
}
