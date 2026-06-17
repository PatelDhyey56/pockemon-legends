using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class AppSharing
{
    private static AppSharing instance = null;

#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void _TAG_ShareSimpleText(string message);
#endif
    public static AppSharing GetInstance()
    {
        if (instance == null)
            instance = new AppSharing();
        return instance;
    }

    /// <summary>
    /// Share app link for android
    /// </summary>
    /// <param name="shareText"></param>
    public void ShareApp(string shareText)
    {
#if UNITY_EDITOR
        Debug.LogError("Unity Editor not support sharing....");
#elif UNITY_IOS
       _TAG_ShareSimpleText(shareText);
#elif UNITY_ANDROID

        //var path = GamePlayerPrefs.GetString("path");
        //AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        //AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
        //intentObject.Call<AndroidJavaObject>("setType", "image/*");
        //intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"),
        //shareText + "\n\n" + "\nPlay Store\n" + "https://play.google.com/store/apps/details?id=" + Application.identifier+
        //"\n\n" + "\nApp Store\n" + "https://apps.apple.com/app/SocialGames24/id1468463834");
        //intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND_MULTIPLE"));
        //AndroidJavaObject list = new AndroidJavaObject("java.util.ArrayList");
        //AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
        //AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + path);
        //list.Call<bool>("add", uriObject);
        //intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject);
        //AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        //AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        //AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share your File");
        //currentActivity.Call("startActivity", chooser);


        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
        intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"),
            shareText);
        intentObject.Call<AndroidJavaObject>("setType", "text/plain");
        AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser",
            intentObject, "Share");
        currentActivity.Call("startActivity", chooser);
#endif

    }

    /// <summary>
    /// Open The Store
    /// Pass IOS APP id for ios store
    /// else it will open Android market
    /// </summary>
    /// <param name="iOSAppID"></param>
    public void OnRateNowButtonClick(string url)
    {
        Application.OpenURL(url);
    }


}
