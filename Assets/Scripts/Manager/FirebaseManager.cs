using Firebase.Analytics;
using UnityEngine;

public class FirebaseManager
{
    public static FirebaseManager Instance;

    public static bool IsInitialized = false;

    public static void CheckFireBaseDependency()
    {
        try
        {
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == Firebase.DependencyStatus.Available)
                {
                    // Create and hold a reference to your FirebaseApp,
                    // where app is a Firebase.FirebaseApp property of your application class. 
                    var app = Firebase.FirebaseApp.DefaultInstance;
                    IsInitialized = true;
                    // Set a flag here to indicate whether Firebase is ready to use by your app.
                }
                else
                {
                    UnityEngine.Debug.LogError(System.String.Format(
                      "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                    // Firebase Unity SDK is not safe to use here.
                }
            });
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Splash CheckFireBaseDependency Exception: " + e.Message);
        }
    }

    public static void LogEvent(string eventName)
    {
        try
        {
            if (!IsInitialized)
            {
                return;
            }
            Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName);
            LogHelper.Info("FirebaseManager ", $"Firebase Analytics not enabled. Event: {eventName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Firebase LogEvent Exception: " + e.Message);
        }
    }

    public static void LogEvent(string eventName,params Parameter[] parameter)
    {
        try
        {
            if (!IsInitialized)
            {
                return;
            }
            Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, parameter);
            LogHelper.Info("FirebaseManager ", $"Firebase Analytics not enabled. Event: {eventName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Firebase LogEvent Exception: " + e.Message);
        }
    }
}
