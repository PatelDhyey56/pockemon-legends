using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    private const string StartScenePath = "Assets/Scenes/SplashScene.unity";
    private const string MenuName = "Tools/Always Start From Splash Scene";
    
    // Default to true
    private static bool Enabled
    {
        get => EditorPrefs.GetBool("AlwaysStartFromSplashScene", true);
        set => EditorPrefs.SetBool("AlwaysStartFromSplashScene", value);
    }

    static PlayModeStartScene()
    {
        EditorApplication.delayCall += UpdatePlayModeStartScene;
    }

    [MenuItem(MenuName)]
    private static void ToggleAction()
    {
        Enabled = !Enabled;
        UpdatePlayModeStartScene();
        Debug.Log("Always Start From Splash Scene is now " + (Enabled ? "ENABLED" : "DISABLED"));
    }

    [MenuItem(MenuName, true)]
    private static bool ToggleActionValidate()
    {
        Menu.SetChecked(MenuName, Enabled);
        return true;
    }

    private static void UpdatePlayModeStartScene()
    {
        if (Enabled)
        {
            SceneAsset startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
            if (startScene != null)
            {
                EditorSceneManager.playModeStartScene = startScene;
            }
            else
            {
                Debug.LogWarning("Could not find SplashScene at " + StartScenePath);
            }
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
