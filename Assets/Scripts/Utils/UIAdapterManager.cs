using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages Canvas scaling across all scenes.
/// Ensures the game UI scales correctly on devices from 5.1" (16:9) to 8" (4:3).
/// Background images are set per-scene in the Editor and are NOT modified at runtime.
/// </summary>
public class UIAdapterManager : MonoBehaviour
{
    private static UIAdapterManager _instance;
    private HashSet<Canvas> _processedCanvases = new HashSet<Canvas>();

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

            if (!_processedCanvases.Contains(canvas))
            {
                AdjustCanvasScaler(canvas);
                _processedCanvases.Add(canvas);
            }
            else
            {
                // Re-check scaler match value if resolution changed dynamically
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
        // - Tall screens (aspect < 0.5625): Match Width (0) fills full width, prevents tiny UI.
        // - Wide screens (aspect >= 0.5625): Match Height (1) prevents top/bottom clipping.
        scaler.matchWidthOrHeight = (screenAspect < refAspect) ? 0f : 1f;
    }
}
