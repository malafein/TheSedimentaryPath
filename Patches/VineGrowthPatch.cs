using HarmonyLib;
using System;
using UnityEngine;
using malafein.Valheim.TheSedimentaryPath.Skills;

namespace malafein.Valheim.TheSedimentaryPath.Patches
{
    // ─── Vine (Vineberry / spreading vine segments) ──────────────────────────

    /// <summary>
    /// Register the RPC_AddVineryCredit handler on each Vine ZNetView when it wakes.
    ///
    /// Credit is applied immediately by advancing s_growStart backward — the vine's
    /// UpdateGrow computes how many growth cycles should have occurred based on
    /// (elapsed / growTime), so pulling the clock back makes it grow more in the
    /// very next check rather than waiting for future cycles.
    ///
    /// Multiple watchers each send their own RPC; all contributions stack directly.
    /// </summary>
    [HarmonyPatch(typeof(Vine), "Awake")]
    public static class VineAwakePatch
    {
        public static void Postfix(Vine __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null) return;

            // Signature: (float credit, long watcherPlayerId). The watcher ID is
            // ignored on Vine (vines spread rather than mature; no maturation event
            // to credit). Included for signature uniformity with Plant/Pickable.
            nview.Register<float, long>("RPC_AddVineryCredit", (long sender, float creditReceived, long watcherPlayerId) =>
            {
                if (!nview.IsOwner()) return;
                ZDO zdo = nview.GetZDO();
                if (zdo == null) return;

                // Advance vine growth timer — credit seconds become elapsed time
                long creditTicks = (long)(creditReceived * TimeSpan.TicksPerSecond);
                long growStart = zdo.GetLong(ZDOVars.s_growStart, 0L);
                if (growStart == 0L)
                    growStart = ZNet.instance.GetTime().Ticks - creditTicks;
                else
                    growStart -= creditTicks;
                zdo.Set(ZDOVars.s_growStart, growStart);

                // Accumulate total watch time for hover text (never consumed)
                float total = zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f);
                zdo.Set(VinerySkill.ZdoCreditKey, total + creditReceived);

                // Advance berry respawn if currently picked
                if (zdo.GetBool(ZDOVars.s_picked, false))
                {
                    long pickedTime = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
                    if (pickedTime > 1L)
                    {
                        long advanceTicks = (long)(creditReceived * VinerySkill.MaxBerryRespawnBoost * TimeSpan.TicksPerSecond);
                        zdo.Set(ZDOVars.s_pickedTime, pickedTime - advanceTicks);
                    }
                }

                Log.Debug($"Vine RPC_AddVineryCredit: advanced timer {creditReceived:F2}s (growStart-={creditTicks}), total={total + creditReceived:F1}s");
            });
        }
    }

    // ─── Plant (cultivated saplings) ─────────────────────────────────────────

    /// <summary>
    /// Register the RPC_AddVineryCredit handler on each Plant ZNetView when it wakes.
    ///
    /// Plant growth is based on TimeSincePlanted() = (now - s_plantTime).TotalSeconds.
    /// Pulling s_plantTime backward increases the apparent age, pushing the plant
    /// past its grow threshold sooner.
    /// </summary>
    [HarmonyPatch(typeof(Plant), "Awake")]
    public static class PlantAwakePatch
    {
        public static void Postfix(Plant __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null) return;

            // Signature: (float credit, long watcherPlayerId). watcherPlayerId is
            // appended to the Plant's TSP_watchers ZDO list so PlantGrowPatch can
            // credit Patience in Bloom feat on maturation.
            nview.Register<float, long>("RPC_AddVineryCredit", (long sender, float creditReceived, long watcherPlayerId) =>
            {
                if (!nview.IsOwner()) return;
                ZDO zdo = nview.GetZDO();
                if (zdo == null) return;

                // Advance plant growth timer
                long plantTime = zdo.GetLong(ZDOVars.s_plantTime, 0L);
                if (plantTime > 0L)
                {
                    long creditTicks = (long)(creditReceived * TimeSpan.TicksPerSecond);
                    zdo.Set(ZDOVars.s_plantTime, plantTime - creditTicks);
                }

                // Accumulate total watch time for hover text
                float total = zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f);
                zdo.Set(VinerySkill.ZdoCreditKey, total + creditReceived);

                // Append watcher's player ID to TSP_watchers if not already present.
                if (watcherPlayerId != 0L)
                {
                    string watchers = zdo.GetString(VinerySkill.ZdoWatchersKey, "");
                    string idStr = watcherPlayerId.ToString();
                    bool present = false;
                    if (!string.IsNullOrEmpty(watchers))
                    {
                        foreach (string existing in watchers.Split(','))
                        {
                            if (existing == idStr) { present = true; break; }
                        }
                    }
                    if (!present)
                    {
                        zdo.Set(VinerySkill.ZdoWatchersKey,
                            string.IsNullOrEmpty(watchers) ? idStr : watchers + "," + idStr);
                    }
                }

                Log.Debug($"Plant RPC_AddVineryCredit: advanced timer {creditReceived:F2}s, total={total + creditReceived:F1}s, watcher={watcherPlayerId}");
            });
        }
    }

    // ─── Wild Pickables (berries, mushrooms, cultivated crops) ───────────────

    /// <summary>
    /// Register the RPC_AddVineryCredit handler on plant-like Pickables.
    /// Vine berries are excluded — their Pickable shares the Vine's ZNetView, so
    /// VineAwakePatch already covers them. This patch handles standalone pickables
    /// (wild blueberries, raspberries, mushrooms, carrots, etc.).
    ///
    /// Wild pickables don't have a growth timer; the credit advances their
    /// respawn timer (s_pickedTime) so they recharge faster after being picked.
    /// </summary>
    [HarmonyPatch(typeof(Pickable), "Awake")]
    public static class PickableAwakePatch
    {
        public static void Postfix(Pickable __instance)
        {
            if (!VinerySkill.IsVineryWatchable(__instance)) return;
            // Vine berries share the Vine's ZNetView — VineAwakePatch registers there.
            if (__instance.GetComponentInParent<Vine>() != null) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null) return;

            // Signature: (float credit, long watcherPlayerId). The watcher ID is
            // ignored on standalone Pickables (no maturation event). Included for
            // signature uniformity with Plant/Vine.
            nview.Register<float, long>("RPC_AddVineryCredit", (long sender, float creditReceived, long watcherPlayerId) =>
            {
                if (!nview.IsOwner()) return;
                ZDO zdo = nview.GetZDO();
                if (zdo == null) return;

                // Advance respawn timer if currently picked
                if (zdo.GetBool(ZDOVars.s_picked, false))
                {
                    long pickedTime = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
                    if (pickedTime > 1L)
                    {
                        long advanceTicks = (long)(creditReceived * VinerySkill.MaxBerryRespawnBoost * TimeSpan.TicksPerSecond);
                        zdo.Set(ZDOVars.s_pickedTime, pickedTime - advanceTicks);
                    }
                }

                float total = zdo.GetFloat(VinerySkill.ZdoCreditKey, 0f);
                zdo.Set(VinerySkill.ZdoCreditKey, total + creditReceived);

                Log.Debug($"Pickable RPC_AddVineryCredit: +{creditReceived:F2}s, total={total + creditReceived:F1}s");
            });
        }
    }
}
