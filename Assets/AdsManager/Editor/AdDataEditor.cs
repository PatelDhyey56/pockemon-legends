using UnityEditor;
using AdsManager.ScriptableObjects;

[CustomEditor(typeof(AdData))]
public class AdDataEditor : Editor
{

    [MenuItem("GameSettings/AdsDetails")]
    public static void OpenAdDataInspector()
    {
        Selection.activeObject = AdData.GetInstance();
    }

    [MenuItem("Assets/AdData/Create")]
    public static void CreateInstance()
    {
        Selection.activeObject = AdData.GetInstance();
    }

}
