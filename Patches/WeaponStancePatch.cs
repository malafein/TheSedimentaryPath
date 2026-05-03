using HarmonyLib;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    public static class WeaponStancePatch
    {
        public static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer) return;

            string itemName = item?.m_shared?.m_name;
            if (itemName == null) return;

            if (Plugin.StanceWeapons.TryGetValue(itemName, out IStanceWeapon weapon))
                weapon.ApplyStance();
        }
    }
}
