using HarmonyLib;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // The Holdfast doctrine predicate. The boon's effects (regen, poison
    // wards, wet negation, grasping retaliation) all gate on this — when
    // false, the SE stays in SEMan with its timer ticking down but its
    // effects don't apply.
    //
    // Doctrine (v2 — requires the presence of the Vine; forbids nothing):
    //   1. PRESENCE — wield the vine, or be clad in it. Either passes:
    //      • a vinery weapon in hand (either hand; shields, tools, armor all
    //        legal), including the Root-Strand Coil while its thrown
    //        projectile is live — the vine rope to the lodged spear IS the
    //        grip, just longer;
    //      • the full Root set worn (helmet, chest, legs), regardless of
    //        what's in hand. One piece alone does not pass.
    //   2. ROOTED — standing on natural ground: terrain, worldgen rock,
    //      anything not player-built. Dormant on built pieces, ships, at
    //      sea, swimming.
    //
    // The rooted check strobes (hops, ledges, knockback), so it breaks only
    // after RootedGraceSeconds of continuous non-contact; presence flips
    // instantly. Without the grace the transition messages spam and effects
    // flicker on every jump.
    public static class HoldfastPredicate
    {
        // How long the rooted condition survives without natural-ground
        // contact. Absorbs jumps and knockback; climbing a roof or boarding
        // a boat breaks it.
        public const float RootedGraceSeconds = 1.75f;

        // The full Root set — worn together they carry the vine for you.
        private static readonly int HelmetRootHash     = "HelmetRoot".GetStableHashCode();
        private static readonly int ArmorRootChestHash = "ArmorRootChest".GetStableHashCode();
        private static readonly int ArmorRootLegsHash  = "ArmorRootLegs".GetStableHashCode();

        // Armor slots are protected on Humanoid — same FieldRef convention
        // as AchievementSystem's unarmored check.
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HelmetItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_helmetItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ChestItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_chestItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> LegItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_legItem");

        private static float _lastRootedTime = -999f;

        // Are all conditions for Holdfast's effects to apply currently met?
        // Called per tick from SE_Holdfast on the local player.
        public static bool IsActive(Player player)
        {
            if (player == null) return false;

            if (IsRootedNow(player))
                _lastRootedTime = Time.time;

            if (Time.time - _lastRootedTime > RootedGraceSeconds) return false;
            return HasVinePresence(player);
        }

        // Drop state on world unload so a new session doesn't inherit a
        // stale rooted stamp.
        public static void ClearAll()
        {
            _lastRootedTime = -999f;
        }

        private static bool HasVinePresence(Player player)
        {
            if (IsVineryWeapon(player.RightItem) || IsVineryWeapon(player.LeftItem))
                return true;

            // Thrown Coil: the projectile carries the grip while live, so the
            // reel/return window is the grace period — and it stays truthful
            // if the reel time is ever retuned.
            if (RootSpearProjectile.HasLiveLocalThrow)
                return true;

            return WearsFullRootSet(player);
        }

        private static bool IsVineryWeapon(ItemDrop.ItemData item)
        {
            if (item?.m_dropPrefab == null) return false;
            return TSPVineryWeapons.MatchesHash(item.m_dropPrefab.name.GetStableHashCode());
        }

        private static bool WearsFullRootSet(Player player)
        {
            return WearsPiece(HelmetItemRef(player), HelmetRootHash)
                && WearsPiece(ChestItemRef(player),  ArmorRootChestHash)
                && WearsPiece(LegItemRef(player),    ArmorRootLegsHash);
        }

        private static bool WearsPiece(ItemDrop.ItemData item, int prefabHash)
        {
            if (item?.m_dropPrefab == null) return false;
            return item.m_dropPrefab.name.GetStableHashCode() == prefabHash;
        }

        // Instantaneous ground check: on the ground, and the thing under
        // your feet is the world's own — terrain (including player-terraformed
        // earth; earth is earth), worldgen rock, a location ruin. A piece
        // with a player creator, or anything aboard a ship, is not.
        private static bool IsRootedNow(Player player)
        {
            if (player.IsSwimming() || !player.IsOnGround()) return false;

            Collider ground = player.GetLastGroundCollider();
            if (ground == null) return false;

            if (ground.GetComponent<Heightmap>() != null) return true;

            Piece piece = ground.GetComponentInParent<Piece>();
            if (piece != null && piece.IsPlacedByPlayer()) return false;

            if (ground.GetComponentInParent<Ship>() != null) return false;

            return true;
        }
    }
}
