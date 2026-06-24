using UnityEngine;

/// <summary>
/// Calculates a global UI scale factor and provides safe-area inset helpers.
/// Supports both scene instance properties and automatic static fallback calculation.
/// </summary>
public class AspectRatio : MonoBehaviour
{
    [Range(0, 1)]
    public float matchWidthOrHeight = 0.5f;
    public Vector2 referenceResolution = new Vector2(1080, 1920);

    private static AspectRatio _instance;
    private static bool _staticInitialized = false;
    private static Vector2 _lastScreenSize;

    private static float _scaleFactor = 1f;
    private static Rect _safeArea;
    private static float _maxBoardCellSize = 100f;

    /// <summary>Ratio of current screen to the reference resolution (blended width/height).</summary>
    public static float ScaleFactor
    {
        get
        {
            EnsureInitialized();
            return _scaleFactor;
        }
    }

    /// <summary>Safe-area rect in screen pixels (accounts for notches, home bars, etc.).</summary>
    public static Rect SafeArea
    {
        get
        {
            EnsureInitialized();
            return _safeArea;
        }
    }

    /// <summary>
    /// Maximum cell size (in canvas units) to use for the board, computed so the
    /// board always fits inside the safe area with comfortable margins.
    /// Reference resolution is 1080x1920; board is 8x8 with spacing.
    /// </summary>
    public static float MaxBoardCellSize
    {
        get
        {
            EnsureInitialized();
            return _maxBoardCellSize;
        }
    }

    private const int GRID_COLS = 8;
    private const int GRID_ROWS = 8;
    private const float CELL_SPACING = 6f;

    // Fraction of the screen height (in reference resolution) the board is allowed to occupy.
    // 0.42 = ~42 % → leaves headroom for the player UI panels above and below.
    private const float BOARD_HEIGHT_FRACTION = 0.42f;

    private void Awake()
    {
        _instance = this;
        Recalculate();
    }

    private void Start()
    {
        Recalculate();
    }

    private Vector2 _lastUpdateScreenSize;
    private Rect _lastUpdateSafeArea;

    private void Update()
    {
        Vector2 currentScreen = new Vector2(Screen.width, Screen.height);
        Rect currentSafeArea = Screen.safeArea;
        if (currentScreen != _lastUpdateScreenSize || currentSafeArea != _lastUpdateSafeArea)
        {
            _lastUpdateScreenSize = currentScreen;
            _lastUpdateSafeArea = currentSafeArea;
            Recalculate();
        }
    }

    private static void EnsureInitialized()
    {
        Vector2 currentScreen = new Vector2(Screen.width, Screen.height);
        if (!_staticInitialized || _lastScreenSize != currentScreen)
        {
            if (_instance != null)
            {
                _instance.Recalculate();
            }
            else
            {
                RecalculateStatic(currentScreen);
            }
            _lastScreenSize = currentScreen;
            _staticInitialized = true;
        }
    }

    private void Recalculate()
    {
        _safeArea = Screen.safeArea;

        // ── Scale factor ─────────────────────────────────────────────────────────
        float wRatio = Screen.width / referenceResolution.x;
        float hRatio = Screen.height / referenceResolution.y;
        _scaleFactor = Mathf.Lerp(wRatio, hRatio, matchWidthOrHeight);

        // ── Dynamic cell size ────────────────────────────────────────────────────
        // Work in canvas (reference) units: convert safe-area dimensions.
        float safeW = (_safeArea.width / Screen.width) * referenceResolution.x;
        float safeH = (_safeArea.height / Screen.height) * referenceResolution.y;

        // Maximum cell fitting width-wise (full safe width minus horizontal padding)
        float horizontalPad = 40f; // 20 px each side in reference units
        float availableW = safeW - horizontalPad;
        float cellFromW = (availableW - (GRID_COLS - 1) * CELL_SPACING) / GRID_COLS;

        // Maximum cell fitting height-wise (BOARD_HEIGHT_FRACTION of safe height)
        float availableH = safeH * BOARD_HEIGHT_FRACTION;
        float cellFromH = (availableH - (GRID_ROWS - 1) * CELL_SPACING) / GRID_ROWS;

        // Use the smaller of the two so the board never overflows in either axis.
        // Clamp between 60 (tiny phones) and 130 (large tablets / landscape tablets).
        _maxBoardCellSize = Mathf.Clamp(Mathf.Min(cellFromW, cellFromH), 60f, 130f);
    }

    private static void RecalculateStatic(Vector2 screenSize)
    {
        _safeArea = Screen.safeArea;
        Vector2 refRes = new Vector2(1080, 1920);

        // Dynamic match: 0 for portrait tall screens, 1 for wide screens
        float screenAspect = screenSize.x / screenSize.y;
        float refAspect = refRes.x / refRes.y;
        float dynamicMatch = (screenAspect < refAspect) ? 0f : 1f;

        // ── Scale factor ─────────────────────────────────────────────────────────
        float wRatio = screenSize.x / refRes.x;
        float hRatio = screenSize.y / refRes.y;
        _scaleFactor = Mathf.Lerp(wRatio, hRatio, dynamicMatch);

        // ── Dynamic cell size ────────────────────────────────────────────────────
        float safeW = (_safeArea.width / screenSize.x) * refRes.x;
        float safeH = (_safeArea.height / screenSize.y) * refRes.y;

        float horizontalPad = 40f;
        float availableW = safeW - horizontalPad;
        float cellFromW = (availableW - (GRID_COLS - 1) * CELL_SPACING) / GRID_COLS;

        float availableH = safeH * BOARD_HEIGHT_FRACTION;
        float cellFromH = (availableH - (GRID_ROWS - 1) * CELL_SPACING) / GRID_ROWS;

        _maxBoardCellSize = Mathf.Clamp(Mathf.Min(cellFromW, cellFromH), 60f, 130f);
    }
}
