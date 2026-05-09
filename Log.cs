using BepInEx.Logging;

namespace malafein.Valheim.TheSedimentaryPath
{
    internal static class Log
    {
        private static ManualLogSource _logger;

        internal static void Init(ManualLogSource logger) => _logger = logger;

        internal static void Info(string message)  => _logger.LogInfo(message);
        internal static void Warn(string message)  => _logger.LogWarning(message);
        internal static void Error(string message) => _logger.LogError(message);

        internal static void Debug(string message)
        {
#if DEBUG
            if (Plugin.IsDebugMode)
                _logger.LogDebug(message);
#endif
        }
    }
}
