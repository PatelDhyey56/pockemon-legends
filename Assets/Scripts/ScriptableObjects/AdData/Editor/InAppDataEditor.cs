using IAPPurchasing;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(InAppData))]
public class InAppDataEditor : Editor
{
    [MenuItem("GameSettings/Select InAppData", priority = 2)]
    public static void OpenInspector()
    {
        Selection.activeObject = InAppData.GetInstance();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}