using System;
using UnityEngine;

namespace IAPPurchasing.Utils
{
    public static class IAPDebug
    {
        private const string LOG_PREFIX = "PurchaseManager: ";
        public static bool canPrintLog = true;

        public static void Log(string message)
        {
            if (canPrintLog)
            {
                Debug.Log(DateTime.Now.ToLongTimeString() + " " + LOG_PREFIX + " " + message);
            }
        }

        public static void Log(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.Log(DateTime.Now.ToLongTimeString() + " " + LOG_PREFIX + " " + tag + ": " + message);
            }
        }

        public static void Warning(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.LogWarning(DateTime.Now.ToLongTimeString() + " " + LOG_PREFIX + " " + tag + ": " + message);
            }
        }

        public static void Warning(string message)
        {
            if (canPrintLog)
            {
                Debug.LogWarning(DateTime.Now.ToLongTimeString() + " " +LOG_PREFIX + message);
            }
        }

        public static void Error(string tag, string message)
        {
            if (canPrintLog)
            {
                Debug.LogError(DateTime.Now.ToLongTimeString() + " " + LOG_PREFIX + " " + tag + ": " + message);
            }
        }

        internal static void Error(string message)
        {
            if (canPrintLog)
            {
                Debug.LogError(DateTime.Now.ToLongTimeString() + " " + LOG_PREFIX + " " + message);
            }
        }
    }
}