using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Bindsinew — the Vinery cult's cultivated vine fiber, and the signature
    // ingredient of both vinery weapons (The Furrowing Share, Root-Strand Coil). It is
    // harvested from TENDED green vines (ones grown from a planted seed); see the
    // green-vine repurpose / BindsinewVine for how it drops. "Living, grasping
    // cordage" — the source of the weapons' snare/root/reel identity.
    //
    // Built by cloning MorgenSinew (already a sinewy strand mesh + icon), renamed and
    // tinted green so it reads as vine fiber rather than the Ashlands drop.
    public static class Bindsinew
    {
        // Green, to distinguish from MorgenSinew's pale strand and tie it to the vines.
        private static readonly Color Tint = new Color(0.30f, 0.52f, 0.20f, 1f);

        public static GameObject CreatePrefab()
        {
            GameObject source = ObjectDB.instance?.GetItemPrefab("MorgenSinew");
            if (source == null)
            {
                Log.Error("Bindsinew.CreatePrefab: MorgenSinew not found");
                return null;
            }

            GameObject prefab = Object.Instantiate(source, Plugin.PrefabContainer);
            prefab.name = TSPVineryWeapons.BindsinewPrefab;

            // Green the dropped-item model (cloned materials — MorgenSinew's stay untouched).
            VisualUtil.TintMaterials(prefab, Tint);
            VisualUtil.ZeroEmission(prefab);

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                Log.Error("Bindsinew.CreatePrefab: no ItemDrop on clone");
                return null;
            }

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            shared.m_name        = TSPVineryWeapons.BindsinewName;
            shared.m_description = "$item_bindsinew_desc";
            shared.m_icons       = VisualUtil.TintIcons(shared.m_icons, Tint);

            return prefab;
        }
    }
}
