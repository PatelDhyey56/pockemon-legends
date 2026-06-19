using UnityEngine;

/// <summary>
/// Calculates a global UI scale factor and provides safe-area inset helpers.
/// Attach this to any persistent GameObject (e.g. the Camera) in every scene.
/// </summary>
public class AspectRatio : MonoBehaviour
{
    [Range(0, 1)]
    public float matchWidthOrHeight = 0.5f;
    public Vector2 referenceResolution = new Vector2(1080, 1920);

    /// <summary>Ratio of current screen to the reference resolution (blended width/height).</summary>
    public static float ScaleFactor { get; private set; } = 1f;

    /// <summary>Safe-area rect in screen pixels (accounts for notches, home bars, etc.).</summary>
    public static Rect SafeArea { get; private set; }

    /// <summary>
    /// Maximum cell size (in canvas units) to use for the board, computed so the
    /// board always fits inside the safe area with comfortable margins.
    /// Reference resolution is 1080x1920; board is 8x8 with spacing.
    /// </summary>
    public static float MaxBoardCellSize { get; private set; } = 100f;

    private const int GRID_COLS = 8;
    private const int GRID_ROWS = 8;
    private const float CELL_SPACING = 6f;

    // Fraction of the screen height (in reference resolution) the board is allowed to occupy.
    // 0.42 = ~42 % → leaves headroom for the player UI panels above and below.
    private const float BOARD_HEIGHT_FRACTION = 0.42f;

    private void Awake()
    {
        Recalculate();
    }

    private void Start()
    {
        Recalculate();
    }

#if UNITY_EDITOR
    // Re-evaluate every frame in the editor so changes are immediately visible
    // in the Game view when the resolution is changed.
    private void Update()
    {
        Recalculate();
    }
#endif

    private void Recalculate()
    {
        // ── Scale factor ─────────────────────────────────────────────────────────
        float wRatio = Screen.width  / referenceResolution.x;
        float hRatio = Screen.height / referenceResolution.y;
        ScaleFactor  = Mathf.Lerp(wRatio, hRatio, matchWidthOrHeight);

        // ── Safe area ────────────────────────────────────────────────────────────
        SafeArea = Screen.safeArea;

        // ── Dynamic cell size ────────────────────────────────────────────────────
        // Work in canvas (reference) units: convert safe-area dimensions.
        float safeW = (SafeArea.width  / Screen.width)  * referenceResolution.x;
        float safeH = (SafeArea.height / Screen.height) * referenceResolution.y;

        // Maximum cell fitting width-wise (full safe width minus horizontal padding)
        float horizontalPad = 40f; // 20 px each side in reference units
        float availableW    = safeW - horizontalPad;
        float cellFromW     = (availableW - (GRID_COLS - 1) * CELL_SPACING) / GRID_COLS;

        // Maximum cell fitting height-wise (BOARD_HEIGHT_FRACTION of safe height)
        float availableH    = safeH * BOARD_HEIGHT_FRACTION;
        float cellFromH     = (availableH - (GRID_ROWS - 1) * CELL_SPACING) / GRID_ROWS;

        // Use the smaller of the two so the board never overflows in either axis.
        // Clamp between 60 (tiny phones) and 130 (large tablets / landscape tablets).
        MaxBoardCellSize = Mathf.Clamp(Mathf.Min(cellFromW, cellFromH), 60f, 130f);
    }
}
