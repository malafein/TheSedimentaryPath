namespace malafein.Valheim.TheSedimentaryPath
{
    internal static class Log
    {
        private const string Prefix = "[TheSedimentaryPath]";

        internal static void Info(string message)  => ZLog.Log($"{Prefix} [INFO] {message}");
        internal static void Warn(string message)  => ZLog.LogWarning($"{Prefix} [WARN] {message}");
        internal static void Error(string message) => ZLog.LogError($"{Prefix} [ERROR] {message}");

        internal static void Debug(string message)
        {
#if DEBUG
            if (Plugin.IsDebugMode)
                ZLog.Log($"{Prefix} [DEBUG] {message}");
#endif
        }
    }
}
