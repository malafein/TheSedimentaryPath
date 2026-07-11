using System.Reflection;
using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Journal;
using malafein.Valheim.TheSedimentaryPath.Skills;
using malafein.Valheim.TheSedimentaryPath.World;

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

    // Award 25 XP the first time a player places a Mysterious Rock.
    // Also stamps the placer's ID on the placed rock's ZDO and increments the
    // They That Watch feat (Option A net counter — see also RemoveMysteriousRockPatch).
    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    public static class PlaceMysteriousRockPatch
    {
        private const string UniqueKey       = "TheSedimentaryPath_PlacedMysteriousRock";
        public  const string PlacerIdZdoKey  = "TSP_placer_id";

        // m_placed is a private static List<IPlaced> field populated by PlacePiece.
        private static readonly FieldInfo PlacedField =
            AccessTools.Field(typeof(Player), "m_placed");

        public static void Postfix(Player __instance, Piece piece, bool __result)
        {
            if (!__result || piece == null)
                return;

            if (Utils.GetPrefabName(piece.gameObject) != "Placeable_HardRock")
                return;

            // Find the just-instantiated piece via the static m_placed list and
            // stamp the placer's PlayerID on its ZDO. PlayerID is stable across
            // sessions (unlike ZDOID, which is re-generated per spawn).
            if (__instance == Player.m_localPlayer)
            {
                var placedList = PlacedField?.GetValue(null) as System.Collections.IEnumerable;
                Piece placedInstance = null;
                if (placedList != null)
                {
                    foreach (object p in placedList)
                    {
                        if (p is Piece pc && Utils.GetPrefabName(pc.gameObject) == "Placeable_HardRock")
                        {
                            placedInstance = pc;
                            break;
                        }
                    }
                }

                ZNetView nview = placedInstance?.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    nview.GetZDO().Set(PlacerIdZdoKey, __instance.GetPlayerID());
                    FeatTracker.RecordEvent(__instance, Feats.MysteriousRocksPlaced);

                    // The Far Placing — distinct biome a Mysterious Rock has
                    // ever been placed in. Set semantic: a later removal of
                    // the rock does not drop the biome from the set.
                    Heightmap.Biome biome = Heightmap.FindBiome(placedInstance.transform.position);
                    if (biome != Heightmap.Biome.None)
                        FeatTracker.AddDistinct(__instance, Feats.RocksInDistantLands, ((int)biome).ToString());

                    // It Rests Easy — placing a Watcher atop the world's highest
                    // peak (same tight 4 m threshold as the pilgrimage).
                    // RecordPersonalBest(..., 1) fires the feat + lore once.
                    if (PeakArrival.IsAtPeak(placedInstance.transform.position))
                        FeatTracker.RecordPersonalBest(__instance, Feats.WatcherAtPeak, 1);
                }
                else
                {
                    Log.Warn("PlaceMysteriousRock: could not find placed-piece ZNetView; feat not credited");
                }
            }

            if (__instance.HaveUniqueKey(UniqueKey))
            {
                Log.Debug("PlaceMysteriousRock: already placed before, skipping XP");
                return;
            }

            __instance.AddUniqueKey(UniqueKey);
            __instance.RaiseSkill(RockerySkill.SkillType, 25f);
            Log.Info("PlaceMysteriousRock: first placement, awarded 25 XP");
        }
    }

    // Decrements the They That Watch feat when the local player removes a
    // Mysterious Rock they originally placed (matched by ZDO placer-ID stamp).
    // Natural destruction (troll, decay) doesn't go through RemovePiece, so
    // those don't decrement the counter.
    //
    // The placer-ID check must run in the prefix, while the rock is still
    // alive: the Mysterious Rock is a Character, so Player.RemovePiece removes
    // it via Character.Damage(1e10) rather than ZNetScene.Destroy. By the time
    // the postfix runs the rock's ZNetView is already invalid, so a postfix ZDO
    // read would find nothing and the decrement would silently no-op (the bug
    // that let place → deconstruct → place inflate the net count).
    [HarmonyPatch(typeof(Player), "RemovePiece")]
    public static class RemoveMysteriousRockPatch
    {
        private static readonly AccessTools.FieldRef<Player, int> RemoveRayMaskRef =
            AccessTools.FieldRefAccess<Player, int>("m_removeRayMask");

        // __state == true means the raycast targeted a Mysterious Rock this
        // player placed; the postfix decrements only once removal succeeds.
        public static void Prefix(Player __instance, out bool __state)
        {
            __state = false;
            if (__instance != Player.m_localPlayer) return;
            if (GameCamera.instance == null || __instance.m_eye == null) return;

            int removeMask = RemoveRayMaskRef(__instance);

            if (!Physics.Raycast(GameCamera.instance.transform.position,
                                 GameCamera.instance.transform.forward,
                                 out RaycastHit hitInfo,
                                 50f,
                                 removeMask))
                return;
            if (Vector3.Distance(hitInfo.point, __instance.m_eye.position) >= __instance.m_maxPlaceDistance)
                return;

            Piece piece = hitInfo.collider.GetComponentInParent<Piece>();
            if (piece == null || Utils.GetPrefabName(piece.gameObject) != "Placeable_HardRock")
                return;

            ZNetView nview = piece.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            long placerId = nview.GetZDO().GetLong(PlaceMysteriousRockPatch.PlacerIdZdoKey, 0L);
            __state = placerId == __instance.GetPlayerID();
        }

        public static void Postfix(Player __instance, bool __result, bool __state)
        {
            if (!__result || !__state) return;
            FeatTracker.RecordEvent(__instance, Feats.MysteriousRocksPlaced, -1);
        }
    }

    // Award 0.25 XP when petting the Mysterious Rock
    [HarmonyPatch(typeof(Pet), nameof(Pet.Interact))]
    public static class PetMysteriousRockPatch
    {
        public static void Postfix(Pet __instance, Humanoid user, bool hold, bool alt, bool __result)
        {
            if (!__result || hold)
                return;

            Player player = user as Player;
            if (player == null)
                return;

            player.RaiseSkill(RockerySkill.SkillType, 0.25f);
            Log.Debug("PetMysteriousRock: petted the rock, awarded 0.25 XP");

            // Shrine pilgrimage hint: only the Mysterious Rock leans the view
            // toward the world's highest peak (and unlocks the tease lore), and
            // only on the regular pet interaction — NOT the alt interaction
            // (Shift+E), which removes items attached to the rock. Sweeping the
            // camera on Shift+E pointed the crosshair away and broke removal.
            if (!alt && Utils.GetPrefabName(__instance.gameObject) == RockShrine.RockPrefabName)
                PeakGuide.Offer(player, __instance.transform.position);
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
            Log.Debug("MysteriousRockSpeak: the Rock has spoken, awarded 25 XP");
        }
    }

    // Debug hover text: shrine score and next-check countdown
    [HarmonyPatch(typeof(Pet), nameof(Pet.GetHoverText))]
    public static class RockShrineHoverPatch
    {
        private static readonly System.Collections.Generic.HashSet<int> s_logged
            = new System.Collections.Generic.HashSet<int>();

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pet __instance, ref string __result)
        {
            if (!Plugin.IsDebugMode) return;
            if (Utils.GetPrefabName(__instance.gameObject) != RockShrine.RockPrefabName) return;

            // Dump hierarchy and Animator info once per rock instance
            int id = __instance.gameObject.GetInstanceID();
            if (s_logged.Add(id))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[RockDebug] Placeable_HardRock children:");
                foreach (Transform child in __instance.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (child == __instance.transform) continue;
                    int depth = 0;
                    for (Transform p = child.parent; p != null && p != __instance.transform; p = p.parent)
                        depth++;
                    sb.AppendLine($"  {new string('-', depth)} {child.name} (active={child.gameObject.activeSelf})");
                }

                var anim = __instance.GetComponentInChildren<Animator>(includeInactive: true);
                if (anim != null)
                {
                    sb.AppendLine($"[RockDebug] Animator: {anim.name} | controller={anim.runtimeAnimatorController?.name}");
                    if (anim.runtimeAnimatorController != null)
                        foreach (var param in anim.parameters)
                            sb.AppendLine($"  param: {param.name} ({param.type})");
                }
                else
                {
                    sb.AppendLine("[RockDebug] No Animator found.");
                }

                Log.Debug(sb.ToString());
            }

            int score = RockShrine.ComputeScore(__instance.transform.position, __instance.gameObject);
            float t      = Mathf.Clamp01((float)(score - RockShrine.MinConversionScore) / (RockShrine.MaxConversionScore - RockShrine.MinConversionScore));
            float chance = Mathf.Lerp(RockShrine.MinConversionChance, RockShrine.MaxConversionChance, t);

            string scoreColor = score >= RockShrine.MinConversionScore ? "#0F0" : "#F80";
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
#if DEBUG
                    float baseInterval = Plugin.ShrineIntervalDebug != null && Plugin.ShrineIntervalDebug.Value > 0f
                        ? Plugin.ShrineIntervalDebug.Value : RockShrine.DefaultInterval;
#else
                    float baseInterval = RockShrine.DefaultInterval;
#endif
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
