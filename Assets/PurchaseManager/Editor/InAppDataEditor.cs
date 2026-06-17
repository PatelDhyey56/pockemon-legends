using UnityEditor;
using IAPPurchasing;

[CustomEditor(typeof(InAppData))]
public class InAppDataEditor : Editor
{
    [MenuItem("GameSettings/InAppData", priority = 1)]
    public static void OpenInspector()
    {
        Selection.activeObject = InAppData.GetInstance();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}