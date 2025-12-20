using System;
using System.Diagnostics;

namespace RingLib
{
    internal class Log
    {
        public static Action<string> LoggerInfo = UnityEngine.Debug.Log;
        public static Action<string> LoggerError = UnityEngine.Debug.LogError;

        public static void LogInfo(string key, string message)
        {
            LoggerInfo($"{key}: {message}");
        }

        public static void LogError(string key, string message)
        {
            if (LoggerError == null)
            {
                return;
            }
            StackTrace stackTrace = new StackTrace();
            LoggerError($"{key}: {stackTrace}\n{message}");
        }
    }
}
