using AdsManager.ScriptableObjects;
using IAPPurchasing;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
public enum ProjectEnvironment
{
    Debug,
    Release
}
[CreateAssetMenu(fileName = "GameSettings", menuName = "Create GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("BUILD MODE")]
    public ProjectEnvironment projectEnvironment;
    [Header("GAME NAME")]
    [SerializeField] private string debugGameName;
    [SerializeField] private string releaseGameName;
    [SerializeField] private string popupTitle;
    [SerializeField] private AdData adData;
    [SerializeField] private InAppData inAppData;
    [SerializeField] private string iOSAppID;

    private const string SettingsFileName = "GameSettings";
    private const string SettingsFileExtension = ".asset";
    private const string ResDir = "Assets/Resources";

    public static GameSettings GetInstance()
    {
        //Read from resources.
        var instance = Resources.Load<GameSettings>(SettingsFileName);
        //Create instance if null.
        if (instance == null)
        {
            Directory.CreateDirectory(ResDir);
            instance = CreateInstance<GameSettings>();
            string assetPath = Path.Combine(ResDir, SettingsFileName + SettingsFileExtension);
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
#endif
        }
        return instance;
    }

    public void UpdateProjectEnvironment()
    {
#if UNITY_EDITOR

        if (projectEnvironment == ProjectEnvironment.Release)
        {
            LogHelper.canPrintLog = false;

            PlayerSettings.productName = releaseGameName;
        }
        else
        {
            LogHelper.canPrintLog = true;

            PlayerSettings.productName = debugGameName;
        }

        AssetDatabase.Refresh();
#endif
    }

    public string GetPlayStoreURL()
    {
        return "https://play.google.com/store/apps/details?id=" + Application.identifier;
    }

    public string GetAppStoreURL()
    {
        return "https://apps.apple.com/app/id" + iOSAppID;
    }

    public string GetGameName()
    {
        if (projectEnvironment == ProjectEnvironment.Release)
        {
            return releaseGameName;
        }
        else
        {
            return debugGameName;
        }
    }

    public bool IsReleaseMode()
    {

        if (projectEnvironment == ProjectEnvironment.Release)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
