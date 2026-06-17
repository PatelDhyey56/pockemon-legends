using System;
using UnityEngine;


public static class LogHelper
{
    private const string LOG_PREFIX = "[UnityBaseProject]";
    public static bool canPrintLog = true;

    public static void Info(string tag, string message)
    {
        if (canPrintLog)
        {
            Debug.Log(LOG_PREFIX + " " + tag + ": " + message);
        }
    }

    public static void Warn(string tag, string message)
    {
        if (canPrintLog)
        {
            Debug.LogWarning(LOG_PREFIX + " " + tag + ": " + message);
        }
    }

    public static void Error(string tag, string message)
    {
        if (canPrintLog)
        {
            Debug.LogError(LOG_PREFIX + " " + tag + ": " + message);
        }
    }

    internal static void Error(string message)
    {
        if (canPrintLog)
        {
            Debug.LogError(LOG_PREFIX + " " + message);
        }
    }
}
