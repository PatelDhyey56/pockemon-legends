using AdsManager.ScriptableObjects;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(AdData))]
public class AdDataEditor : Editor
{
    [MenuItem("GameSettings/Select AdsData", priority = 3)]
    public static void OpenInspector()
    {
        Selection.activeObject = AdData.GetInstance();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}