using System.Collections;
using UnityEngine;

namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Shared deferred center-screen messaging. Journal subsystems
    // (FeatTracker, BoonSystem, LoreChecker) all need to fire
    // MessageHud.Center events that don't get clobbered by simultaneous
    // biome banners, boss intros, or other center events. Default delay
    // is long enough that those banners have cleared first.
    //
    // Pass delay=0 for an immediate message (e.g. ritual transition
    // notifications where the player just performed a deliberate action
    // and is watching for the response).
    public static class Notify
    {
        public const float DefaultDelay = 4f;

        public static void Center(string message, float delay = DefaultDelay)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (delay <= 0f)
            {
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, message);
                return;
            }

            // Coroutine needs a MonoBehaviour host. Fall back to immediate
            // display if Plugin.Instance isn't set yet (very early init —
            // shouldn't happen in practice but keeps the helper safe).
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(ShowDelayed(message, delay));
            else
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, message);
        }

        private static IEnumerator ShowDelayed(string message, float delay)
        {
            yield return new WaitForSeconds(delay);
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, message);
        }
    }
}
