using HarmonyLib;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // Injects custom mead-base → brew conversions into every Fermenter instance.
    [HarmonyPatch(typeof(Fermenter), "Awake")]
    public static class FermenterPatch
    {
        public static void Postfix(Fermenter __instance)
        {
            TryAddConversion(__instance, "BlackstoneBrewBase", "BlackstoneBrew",   producedItems: 6);
            TryAddConversion(__instance, "VineberryJuiceBase", "VineberryJuice",   producedItems: 6);
        }

        private static void TryAddConversion(Fermenter fermenter, string fromName, string toName, int producedItems)
        {
            // Guard: only add if both items are registered and not already present
            var fromPrefab = ObjectDB.instance?.GetItemPrefab(fromName);
            var toPrefab   = ObjectDB.instance?.GetItemPrefab(toName);

            if (fromPrefab == null || toPrefab == null)
            {
                ZLog.LogWarning($"[TheSedimentaryPath] FermenterPatch: skipping {fromName} → {toName} (prefabs not yet registered)");
                return;
            }

            foreach (var existing in fermenter.m_conversion)
            {
                if (existing.m_from?.gameObject.name == fromName)
                    return; // already injected
            }

            fermenter.m_conversion.Add(new Fermenter.ItemConversion
            {
                m_from         = fromPrefab.GetComponent<ItemDrop>(),
                m_to           = toPrefab.GetComponent<ItemDrop>(),
                m_producedItems = producedItems,
            });

            ZLog.Log($"[TheSedimentaryPath] FermenterPatch: added {fromName} → {toName} ×{producedItems}");
        }
    }
}
