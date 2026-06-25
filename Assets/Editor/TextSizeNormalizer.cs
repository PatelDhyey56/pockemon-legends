using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Normalizes all TMP and Legacy Text font sizes across all scenes.
/// Rules:
///   - Groups texts by their immediate UI panel/parent container.
///   - Finds the minimum font size in the group.
///   - If min < 20, scales ALL texts in the group proportionally so min becomes 20.
///   - Any text that is already >= 20 and is NOT in a group with a sub-20 text stays untouched.
/// </summary>
public class TextSizeNormalizer : EditorWindow
{
    private const float MIN_SIZE = 20f;

    [MenuItem("Tools/Normalize Text Sizes (Min 20, Proportional)")]
    public static void RunNormalization()
    {
        if (!EditorUtility.DisplayDialog(
            "Normalize Text Sizes",
            "This will scale all TMP and Legacy Text font sizes across ALL scenes.\n" +
            "- Min font size will be 20.\n" +
            "- Proportional ratios within each UI group will be maintained.\n\n" +
            "All modified scenes will be saved. Proceed?",
            "Yes, Normalize", "Cancel"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        string[] scenePaths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();

        int totalChanged = 0;

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"[TextNormalizer] Processing scene: {scene.name}");

            int changed = ProcessScene();
            totalChanged += changed;

            if (changed > 0)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[TextNormalizer] Saved {scene.name} — {changed} text(s) modified.");
            }
            else
            {
                Debug.Log($"[TextNormalizer] {scene.name} — no changes needed.");
            }
        }

        EditorUtility.DisplayDialog("Done", $"Text normalization complete.\n{totalChanged} text component(s) updated across all scenes.", "OK");
    }

    private static int ProcessScene()
    {
        // Collect all TMP texts
        var allTMP = Object.FindObjectsOfType<TMP_Text>(true);
        // Collect all legacy texts
        var allLegacy = Object.FindObjectsOfType<Text>(true);

        // Build groups by grouping parent (the grandparent GameObject — e.g., the popup/panel container)
        // We use the 2nd-level ancestor as the group key so texts inside the same popup are grouped together.
        // If no grandparent, use the direct parent. If no parent, use the object itself.
        var groups = new Dictionary<Transform, List<TextEntry>>();

        foreach (var t in allTMP)
        {
            var key = GetGroupKey(t.transform);
            if (!groups.ContainsKey(key)) groups[key] = new List<TextEntry>();
            groups[key].Add(new TextEntry { tmpText = t, isTMP = true });
        }
        foreach (var t in allLegacy)
        {
            var key = GetGroupKey(t.transform);
            if (!groups.ContainsKey(key)) groups[key] = new List<TextEntry>();
            groups[key].Add(new TextEntry { legacyText = t, isTMP = false });
        }

        int changed = 0;

        foreach (var kvp in groups)
        {
            var entries = kvp.Value;

            // Get current sizes
            float minSize = float.MaxValue;
            foreach (var e in entries)
            {
                float sz = e.isTMP ? e.tmpText.fontSize : e.legacyText.fontSize;
                if (sz < minSize) minSize = sz;
            }

            // Only scale up if min < MIN_SIZE
            if (minSize < MIN_SIZE)
            {
                float scaleFactor = MIN_SIZE / minSize;

                foreach (var e in entries)
                {
                    if (e.isTMP)
                    {
                        float oldSize = e.tmpText.fontSize;
                        float newSize = Mathf.Round(oldSize * scaleFactor);
                        if (Mathf.Abs(newSize - oldSize) > 0.01f)
                        {
                            Undo.RecordObject(e.tmpText, "Normalize Text Size");
                            e.tmpText.fontSize = newSize;
                            e.tmpText.enableAutoSizing = false;
                            EditorUtility.SetDirty(e.tmpText);
                            changed++;
                            Debug.Log($"[TextNormalizer] TMP '{e.tmpText.name}' (group '{kvp.Key.name}'): {oldSize} → {newSize}");
                        }
                    }
                    else
                    {
                        float oldSize = e.legacyText.fontSize;
                        float newSize = Mathf.Round(oldSize * scaleFactor);
                        if (Mathf.Abs(newSize - oldSize) > 0.01f)
                        {
                            Undo.RecordObject(e.legacyText, "Normalize Text Size");
                            e.legacyText.fontSize = Mathf.RoundToInt(newSize);
                            e.legacyText.resizeTextForBestFit = false;
                            EditorUtility.SetDirty(e.legacyText);
                            changed++;
                            Debug.Log($"[TextNormalizer] Legacy '{e.legacyText.name}' (group '{kvp.Key.name}'): {oldSize} → {newSize}");
                        }
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Returns the grouping key for a transform.
    /// Uses 2 levels up from the text component — e.g., the popup panel root.
    /// This ensures texts inside the same popup/dialog are grouped together.
    /// </summary>
    private static Transform GetGroupKey(Transform t)
    {
        // Go up 2 levels: text → row/cell → panel/popup
        Transform key = t;
        if (key.parent != null) key = key.parent;    // 1 level up
        if (key.parent != null) key = key.parent;    // 2 levels up
        return key;
    }

    private class TextEntry
    {
        public TMP_Text tmpText;
        public Text legacyText;
        public bool isTMP;
    }
}
