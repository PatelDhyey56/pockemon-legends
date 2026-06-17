using System;
using UnityEngine;

namespace AdsManager.Utils
{
    public static class AdsDebug
    {
        private const string DEFAULT_TAG = "AdMobManager";
        public static bool canPrintLog = true;

        public static void Log(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.Log(DateTime.Now.ToLongTimeString() + " " + tag + ": " + message);
            }
        }

        public static void Log(string message)
        {
            if (canPrintLog)
            {
                Debug.Log(DateTime.Now.ToLongTimeString() + " " + DEFAULT_TAG + ": " + message);
            }
        }

        public static void Warning(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.LogWarning(DateTime.Now.ToLongTimeString() + " " + tag + ": " + message);
            }
        }

        public static void Warning(string message)
        {
            if (canPrintLog)
            {
                Debug.LogWarning(DateTime.Now.ToLongDateString() + " " + DEFAULT_TAG + ": " + message);
            }
        }

        public static void Error(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.LogError(DateTime.Now.ToLongTimeString() + " " + tag + ": " + message);
            }
        }

        public static void LogError(string message)
        {
            if (canPrintLog)
            {
                Debug.LogError(DateTime.Now.ToLongTimeString() + " " + DEFAULT_TAG + " " + message);
            }
        }
    }
}
