using HarmonyLib;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Items
{
    // Rides on the RootSpear's thrown vine projectile and owns two things: the Vault
    // stance's self-pull, and returning the spear to the thrower's hand.
    //
    // The throw CONSUMES the spear (m_consumeItem), so it leaves your hand and becomes
    // this projectile — the spear "on a vine" (the projectile's LineConnect rope ties it
    // to you). Where it ends up:
    //   • stuck in a creature → Cast reels the creature to you (SE_VineTether), Vault
    //     reels you to the creature (the self-pull below)
    //   • stuck in the ground (a miss) → it's the landed spear; walk over to it
    // In every case the spear returns to your hand when you close to the RETRIEVE POINT
    // (the reeled creature, or — on a miss — the spear itself), or when the reel window
    // elapses (the vine yanks it home). An OnDestroy safety net guarantees a thrown
    // spear is never lost.
    //
    // All of this runs only on the thrower's client: Projectile.m_onHit fires under the
    // projectile's owner, which is the thrower, where Player.m_localPlayer IS the thrower.
    public class RootSpearProjectile : MonoBehaviour
    {
        // Vault self-pull — a grappling hook. Yanks the player toward wherever the spear
        // stuck (creature, ground, tree, anything). VelocityChange (mass-independent):
        // the player's mass is constant, so a reliable, quick pull. Per-mob mass
        // resistance lives on the Cast creature reel (SE_VineTether), not here.
        public float m_pullSpeed      = 32f;  // quick grapple
        public float m_reelToDistance = 2.5f;
        public float m_smoothDistance = 2f;
        public float m_forcePower     = 1f;   // near-linear falloff — stays strong most of the way
        public float m_maxUpSpeed     = 8f;   // keep some lift (climb ledges) but don't rocket skyward
        public float m_arrivalDamping = 0.5f; // on arrival, bleed the last momentum
        public float m_easePower      = 0.5f; // <1: fast most of the way, decel steepens as you close in
        public float m_minGrappleSpeed = 4f;  // don't crawl at the very end (arrival handles the stop)

        // Return once the player is this close to the retrieve point, or after the reel
        // window elapses (whichever comes first).
        public float m_returnDistance = 3f;
        public float m_maxReelTime    = 2.5f;

        // Each thrown spear carries its own source weapon in Projectile.m_weapon (private).
        // We clone THAT item at spawn and hand the clone back, so every throw returns its
        // own spear with all instance data intact — quality, durability, variant, and the
        // original crafter (not assumed to be the thrower). No shared/static slot, so
        // throwing several different spears returns each correctly.
        private static readonly AccessTools.FieldRef<Projectile, ItemDrop.ItemData> WeaponRef =
            AccessTools.FieldRefAccess<Projectile, ItemDrop.ItemData>("m_weapon");

        private Projectile m_projectile;
        private ZNetView m_znview;
        private bool m_ownerResolved;
        private bool m_isOwner;
        private bool m_owesReturn; // consumed at throw; must give the spear back (owner only)
        private bool m_returned;
        private bool m_vault;      // thrown in Vault stance (self-pull) vs Cast
        private bool m_hasHit;     // the spear has landed/stuck (creature or ground)
        private Transform m_target; // creature the spear stuck into (null on a miss)
        private float m_timer;
        private float m_grappleStartDist = 1f; // distance at hookup, for the ease taper
        private ItemDrop.ItemData m_returnItem; // clone of the thrown spear, given back on return

        private void Awake()
        {
            m_projectile = GetComponent<Projectile>();
            m_znview     = GetComponent<ZNetView>();
            if (m_projectile != null)
                m_projectile.m_onHit += OnProjectileHit;

            // Stance is captured at spawn (throw time), not later, so a toggle mid-flight
            // can't change how this in-flight spear behaves.
            m_vault = Plugin.StanceWeapons.TryGetValue(TSPVineryWeapons.RootSpearName, out IStanceWeapon w)
                      && w is RootSpear s && s.IsVaultStance;
        }

        private void OnDestroy()
        {
            if (m_projectile != null)
                m_projectile.m_onHit -= OnProjectileHit;
            // Safety net: never lose a thrown spear. If we still owe a return (owner,
            // consumed at throw, not yet given back), return it now.
            if (m_owesReturn && !m_returned)
                GiveSpearBack();
        }

        private void ResolveOwner()
        {
            if (m_ownerResolved || m_znview == null || !m_znview.IsValid()) return;
            m_ownerResolved = true;
            m_isOwner    = m_znview.IsOwner();
            m_owesReturn = m_isOwner; // on the thrower's client we own it and owe the spear

            // Capture the exact spear that was thrown (Projectile.Setup has run by now, so
            // m_weapon is set). Clone it so the return preserves all instance data.
            if (m_isOwner && m_projectile != null)
            {
                ItemDrop.ItemData weapon = WeaponRef(m_projectile);
                if (weapon != null)
                {
                    m_returnItem = weapon.Clone();
                    m_returnItem.m_stack = 1;
                }
            }
        }

        private void OnProjectileHit(Collider collider, Vector3 hitPoint, bool water)
        {
            if (water) return;
            m_hasHit = true; // landed — stuck in a creature or the ground

            // Distance at hookup drives the Vault ease taper.
            Player p = Player.m_localPlayer;
            if (p != null)
                m_grappleStartDist = Mathf.Max(Vector3.Distance(p.transform.position, hitPoint), m_reelToDistance + 0.1f);

            Character target = collider != null ? collider.GetComponentInParent<Character>() : null;
            if (target != null && target != Player.m_localPlayer && !target.IsBoss())
                m_target = target.transform;
        }

        private void FixedUpdate()
        {
            ResolveOwner();
            if (!m_isOwner || m_returned) return;

            Player player = Player.m_localPlayer;
            if (player == null) return;
            m_timer += Time.fixedDeltaTime;

            // Vault: grapple the player toward wherever the spear stuck (the projectile
            // itself is the anchor — it tracks a creature if it stuck one, or sits at the
            // ground/static hit point). noUpForce:false so it can pull you up onto ledges.
            if (m_vault && m_hasHit)
            {
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null && player.GetStandingOnShip() == null && !player.IsAttached())
                {
                    Vector3 toAnchor = transform.position - player.transform.position;
                    float dist = toAnchor.magnitude;
                    if (dist > m_reelToDistance)
                    {
                        // Velocity tapers with remaining distance on a curve (t^power, power<1):
                        // strong most of the way, decelerating more steeply as you close in — a
                        // clean ease that settles at the anchor instead of overshooting.
                        float span  = Mathf.Max(m_grappleStartDist - m_reelToDistance, 0.1f);
                        float t     = Mathf.Clamp01((dist - m_reelToDistance) / span);
                        float speed = Mathf.Max(m_pullSpeed * Mathf.Pow(t, m_easePower), m_minGrappleSpeed);
                        Vector3 vel = toAnchor.normalized * speed;
                        if (vel.y > m_maxUpSpeed) vel.y = m_maxUpSpeed; // some lift, no rocket
                        rb.velocity = vel;
                    }
                    else
                    {
                        // Arrived — bleed off the last of the momentum (a little overshoot is fun).
                        rb.velocity *= m_arrivalDamping;
                    }
                }
            }

            // Proximity-return is only allowed once the spear has landed/stuck — the
            // projectile spawns AT the player, so checking it in flight would return the
            // spear instantly (it would never appear to leave your hand). Retrieve point:
            // the reeled creature if we stuck one, else the landed spear itself.
            bool canReturnByProximity = m_target != null || m_hasHit;
            Vector3 retrievePoint = m_target != null ? m_target.position : transform.position;
            bool reached = canReturnByProximity &&
                Vector3.Distance(player.transform.position, retrievePoint) <= m_returnDistance;
            if (reached || m_timer >= m_maxReelTime)
            {
                GiveSpearBack();
                DestroyProjectile();
            }
        }

        private void GiveSpearBack()
        {
            if (m_returned) return;
            m_returned   = true;
            m_owesReturn = false;

            Player player = Player.m_localPlayer;
            if (player == null) return;

            // Fallback to a fresh spear only if the clone was never captured (shouldn't
            // happen — Setup runs before the first FixedUpdate).
            ItemDrop.ItemData item = m_returnItem;
            if (item == null)
            {
                if (Plugin.RootSpearPrefab == null) return;
                item = Plugin.RootSpearPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                item.m_stack = 1;
            }

            Inventory inv = player.GetInventory();
            if (inv != null && inv.AddItem(item))
            {
                // Yank it back into an empty hand. GetCurrentWeapon() falls back to the
                // unarmed weapon (never null), so "empty" means it equals that.
                ItemDrop.ItemData current = player.GetCurrentWeapon();
                bool handEmpty = current == null
                    || (player.m_unarmedWeapon != null && current == player.m_unarmedWeapon.m_itemData);
                if (handEmpty)
                    player.EquipItem(item);
            }
            else
            {
                // Inventory full — drop it at the player's feet so it's never lost.
                ItemDrop.DropItem(item, 1, player.transform.position + Vector3.up, Quaternion.identity);
            }
        }

        private void DestroyProjectile()
        {
            if (m_znview != null && m_znview.IsValid() && m_znview.IsOwner() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
        }
    }
}
