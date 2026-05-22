using malafein.Valheim.TheSedimentaryPath.Items;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // The Stone-Kin doctrine predicate. Stone-Kin's effects (damage
    // resistance, knockback reduction, and the tier-3 KinFist) all gate
    // on this — when false, the SE stays in SEMan with its timer
    // ticking down but its effects don't apply.
    //
    // Doctrine: player must be UNARMORED (no helmet, chest, or legs)
    // AND both hands must be either bare or holding one of the four
    // TSP rockery weapons. Anything else — torch, hammer, shield, bow,
    // non-TSP weapon — breaks the doctrine.
    public static class StoneKinPredicate
    {
        // Bare hand or one of the four TSP rockery weapons. Anything else
        // breaks the doctrine.
        public static bool IsKinHandEquipped(ItemDrop.ItemData item)
        {
            if (item == null) return true;
            if (item.m_dropPrefab == null) return false;
            return TSPRockeryWeapons.MatchesHash(item.m_dropPrefab.name.GetStableHashCode());
        }

        // Are all conditions for Stone-Kin's effects to apply currently met?
        public static bool IsActive(Player player)
        {
            if (player == null) return false;
            if (!AchievementSystem.IsUnarmored(player)) return false;
            if (!IsKinHandEquipped(player.RightItem)) return false;
            if (!IsKinHandEquipped(player.LeftItem)) return false;
            return true;
        }
    }
}
