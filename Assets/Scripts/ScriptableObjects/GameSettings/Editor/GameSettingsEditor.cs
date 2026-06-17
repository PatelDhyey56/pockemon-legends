using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(GameSettings))]
public class GameSettingsEditor : Editor
{
    [MenuItem("GameSettings/Open Settings", priority = 1)]
    public static void OpenInspector()
    {
        Selection.activeObject = GameSettings.GetInstance();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GameSettings projectSettings = (GameSettings)target;

        if (GUILayout.Button("UPDATE MODE"))
        {
            projectSettings.UpdateProjectEnvironment();
        }
    }
}