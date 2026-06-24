using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically manages Canvas scaling and dynamic background overrides across all scenes.
/// Ensures the game scales correctly on devices from 5.1" (16:9) to 8" (4:3)
/// and applies a custom mythical war background image to all main backgrounds at runtime.
/// </summary>
public class UIAdapterManager : MonoBehaviour
{
    private static UIAdapterManager _instance;
    private HashSet<Canvas> _processedCanvases = new HashSet<Canvas>();
    private static Sprite _loadedBackgroundSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        GameObject managerGo = new GameObject("UIAdapterManager");
        _instance = managerGo.AddComponent<UIAdapterManager>();
        DontDestroyOnLoad(managerGo);
        
        // Also attach AspectRatio to ensure it is always recalculating dynamically
        if (managerGo.GetComponent<AspectRatio>() == null)
        {
            managerGo.AddComponent<AspectRatio>();
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _processedCanvases.Clear();
        ProcessAllCanvases();
    }

    private Vector2 _lastScreenSize;

    private void Start()
    {
        _lastScreenSize = new Vector2(Screen.width, Screen.height);
        ProcessAllCanvases();
    }

    private void Update()
    {
        Vector2 currentScreen = new Vector2(Screen.width, Screen.height);
        if (currentScreen != _lastScreenSize)
        {
            _lastScreenSize = currentScreen;
            ProcessAllCanvases();
        }
    }

    private void ProcessAllCanvases()
    {
#if UNITY_2023_1_OR_NEWER
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
#endif

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null) continue;

            // Only process root screen-space canvases
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;

            // Process canvas if not already processed
            if (!_processedCanvases.Contains(canvas))
            {
                AdjustCanvasScaler(canvas);
                ApplyDynamicBackground(canvas);
                _processedCanvases.Add(canvas);
            }
            else
            {
                // Verify scaler match value in case resolution changed dynamically
                UpdateCanvasScalerMatch(canvas);
            }
        }
    }

    private void AdjustCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        UpdateCanvasScalerMatch(canvas);
    }

    private void UpdateCanvasScalerMatch(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float refAspect = 1080f / 1920f;

        // Dynamic Match Width or Height:
        // - Tall screens (aspect < 0.5625): Match Width (0) so elements scale up to occupy full width (preventing tiny UI).
        // - Wide screens (aspect >= 0.5625): Match Height (1) so elements scale to fit vertically (preventing top/bottom clipping).
        scaler.matchWidthOrHeight = (screenAspect < refAspect) ? 0f : 1f;
    }

    private void ApplyDynamicBackground(Canvas canvas)
    {
        Sprite bgSprite = GetLoadedBackgroundSprite();
        if (bgSprite == null) return;

        bool found = ApplyBackgroundRecursive(canvas.transform, bgSprite);
        if (!found && ShouldCreateBackgroundForCanvas(canvas))
        {
            CreateBackgroundForCanvas(canvas, bgSprite);
        }
    }

    private bool ApplyBackgroundRecursive(Transform parent, Sprite bgSprite)
    {
        bool foundAny = false;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            
            if (IsMainBackground(child))
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = bgSprite;
                    img.color = Color.white;
                    foundAny = true;
                }
            }
            
            // Continue search recursively in case backgrounds are nested inside panel containers
            if (ApplyBackgroundRecursive(child, bgSprite))
            {
                foundAny = true;
            }
        }
        return foundAny;
    }

    private bool IsMainBackground(Transform child)
    {
        string nameLower = child.name.ToLower();
        return nameLower == "background" || 
               nameLower == "bg" || 
               nameLower == "globalbackground" || 
               nameLower == "mainbackground";
    }

    private bool ShouldCreateBackgroundForCanvas(Canvas canvas)
    {
        string nameLower = canvas.gameObject.name.ToLower();
        
        // Exclude typical overlay/popup/system canvases to prevent obscuring the main screen
        if (nameLower.Contains("loading") ||
            nameLower.Contains("message") ||
            nameLower.Contains("exit") ||
            nameLower.Contains("adfree") ||
            nameLower.Contains("rate") ||
            nameLower.Contains("setting") ||
            nameLower.Contains("popup") ||
            nameLower.Contains("dialog") ||
            nameLower.Contains("toast") ||
            nameLower.Contains("banner") ||
            nameLower.Contains("controller") ||
            nameLower.Contains("system"))
        {
            return false;
        }

        return true;
    }

    private void CreateBackgroundForCanvas(Canvas canvas, Sprite bgSprite)
    {
        // Double check if a child named "MainBackground" already exists so we don't duplicate it
        Transform existing = canvas.transform.Find("MainBackground");
        if (existing != null)
        {
            Image existingImg = existing.GetComponent<Image>();
            if (existingImg != null)
            {
                existingImg.sprite = bgSprite;
                existingImg.color = Color.white;
            }
            return;
        }

        GameObject bgGo = new GameObject("MainBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvas.transform, false);
        bgGo.transform.SetSiblingIndex(0); // Render behind all other UI elements

        RectTransform rect = bgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = bgGo.GetComponent<Image>();
        img.sprite = bgSprite;
        img.color = Color.white;
        img.raycastTarget = false; // Ensure background doesn't block raycasts

        Debug.Log("[UIAdapterManager] Created dynamic background for Canvas: " + canvas.gameObject.name);
    }

    private static Sprite GetLoadedBackgroundSprite()
    {
        if (_loadedBackgroundSprite != null) return _loadedBackgroundSprite;

        string path = "/home/spellenx/Pictures/creture war/game-scene.png";
        if (!System.IO.File.Exists(path))
        {
            // Fallback to battle-scene if game-scene is missing
            path = "/home/spellenx/Pictures/creture war/battle-scene.png";
        }

        if (System.IO.File.Exists(path))
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    // Create sprite wrapping the texture with full screen layout config
                    _loadedBackgroundSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    Debug.Log("[UIAdapterManager] Successfully loaded dynamic background from: " + path);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[UIAdapterManager] Failed to load dynamic background: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[UIAdapterManager] Dynamic background file not found at: " + path);
        }

        return _loadedBackgroundSprite;
    }
}
