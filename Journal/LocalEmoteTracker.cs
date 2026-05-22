namespace malafein.Valheim.TheSedimentaryPath.Journal
{
    // Tracks the local player's most recently started emote.
    //
    // Player.LastEmote is a global static updated by every player's
    // StartEmote, so in multiplayer it gets clobbered by other players'
    // emotes — unusable for "is the local player kneeling?" checks.
    // PlayerStartEmotePatch keeps CurrentEmote here in sync only with the
    // local player.
    //
    // Note on the held kneel pose: vanilla /kneel is a one-shot emote;
    // m_emoteState stays empty (""), but Player.InEmote() returns true
    // throughout the held pose (probe-confirmed). So the kneel-active
    // predicate is `player.InEmote() && CurrentEmote == "kneel"`.
    public static class LocalEmoteTracker
    {
        public const string KneelEmote = "kneel";

        public static string CurrentEmote = "";
        private static bool _wasInEmote;

        public static bool IsKneeling(Player player)
            => player != null
            && player == Player.m_localPlayer
            && player.InEmote()
            && CurrentEmote == KneelEmote;

        // Clear CurrentEmote on the InEmote true→false transition.
        //
        // Transition-based (not "clear whenever InEmote is false") because
        // Player.StartEmote runs in Update and sets CurrentEmote, but
        // Player.UpdateEmote runs in LateUpdate and is what flips InEmote
        // to true. So there's a single-frame window where CurrentEmote is
        // set but InEmote still returns false. Clearing on that frame
        // would zero out CurrentEmote before the emote actually starts.
        //
        // Fast path: when no emote is tracked and we weren't emoting last
        // frame, skip the InEmote call entirely — typical-frame cost is
        // just two bool comparisons. Called from PlayerUpdatePatch.
        public static void Tick(Player player)
        {
            if (CurrentEmote.Length == 0 && !_wasInEmote) return;
            if (player == null) return;

            bool inEmote = player.InEmote();
            if (_wasInEmote && !inEmote)
                CurrentEmote = "";
            _wasInEmote = inEmote;
        }
    }
}
