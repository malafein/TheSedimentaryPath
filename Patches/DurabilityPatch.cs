using System.Reflection;
using HarmonyLib;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    public static class DurabilityPatch
    {
        private static readonly FieldInfo WeaponField =
            AccessTools.Field(typeof(Attack), "m_weapon");

        public static void Postfix(Attack __instance)
        {
            if (!__instance.m_consumeItem)
                return;

            var weapon = (ItemDrop.ItemData)WeaponField?.GetValue(__instance);
            if (weapon == null ||
                (weapon.m_shared.m_name != "$item_heftystone" && weapon.m_shared.m_name != "$item_smoothstone"))
                return;

            // After a throw consumed one from the stack, reset durability to max.
            // The thrown stone was the worn one; the next in the stack is fresh.
            if (weapon.m_stack >= 1)
            {
                weapon.m_durability = weapon.GetMaxDurability();
                ZLog.Log($"[TheSedimentaryPath] DurabilityPatch: reset durability to {weapon.m_durability} after throw (stack={weapon.m_stack})");
            }
        }
    }
}
