using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class BoardInputHandler : MonoBehaviour
{
    [SerializeField] private Transform boardParent;
    [SerializeField] private float cellSize = 100f; // Inspector fallback — overridden at runtime by AspectRatio.MaxBoardCellSize
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Color charryColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color borderColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private float borderWidth = 3f;
    [SerializeField] private Color boardBgColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);

    // Resolved at runtime; driven by AspectRatio.MaxBoardCellSize when available.
    private float _resolvedCellSize;

    // Fast O(1) gem lookup: maps gem GameObject InstanceID → (row,col)
    private Dictionary<int, Vector2Int> _gemCoordMap = new Dictionary<int, Vector2Int>(64);

    // Cached reusable WaitForSeconds objects — avoids GC alloc every coroutine step
    private static readonly WaitForSeconds _wait025 = new WaitForSeconds(0.25f);
    private static readonly WaitForSeconds _wait03  = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _wait035 = new WaitForSeconds(0.35f);
    private static readonly WaitForSeconds _waitSwap = new WaitForSeconds(0.2f); // swap animation duration

    // Pre-built gem colors — matched to the new stone artwork dominant hues
    private static readonly Color[] GemColors = new Color[]
    {
        new Color(0.95f, 0.25f, 0.05f), // Fire     — deep orange-red  (element_gem_2)
        new Color(0.05f, 0.75f, 0.95f), // Water    — vivid cyan-blue  (element_gem_3)
        new Color(0.35f, 0.90f, 0.15f), // Nature   — bright lime      (element_gem_4)
        new Color(1.00f, 0.78f, 0.00f), // Electric — amber-yellow     (element_gem_6)
        new Color(0.70f, 0.15f, 0.95f), // Psychic  — violet-purple    (element_gem_5)
        new Color(0.58f, 0.42f, 0.22f), // Healing  — warm earth-brown (element_gem_1)
        new Color(0.95f, 0.10f, 0.10f), // Evolution — crimson-red Pokéball stone
    };
    private static readonly Color _pipEmptyColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
    private static readonly Color _activePanel   = new Color(0.15f, 0.15f, 0.18f, 0.95f);
    private static readonly Color _inactivePanel = new Color(0.03f, 0.03f, 0.05f, 0.7f);

    // Drag committed flag to suppress redundant OnGemDrag calls after swap fires
    private bool _dragCommitted;

    // Evolution stone sprite — loaded once from Resources/Gems/evolution_stone
    private Sprite _evolutionStoneSprite;
    // Evolution stone highlight color for board cells
    private static readonly Color _evolutionGlow = new Color(0.95f, 0.10f, 0.10f);

    [Header("Visuals")]
    [SerializeField] private Sprite backgroundImage; // The new background image
    [SerializeField] private Sprite[] gemSprites;
    private Sprite fallbackGemSprite;
    private Image[,] gemImages;
    private RectTransform[,] gemRects;
    private RectTransform boardRect;
    private Vector2Int? firstSelection;
    private bool isInitialized;
    private bool isAnimating;

    private float inactivityTime = 0f;
    private bool hintShowing = false;
    private Sequence hintSequence;
    private float _processingWatchdog = 0f; // safety timeout for stuck IsProcessing
    private GameObject _boardBlurOverlay;
    private GameObject _manualEvoPopupInstance;

    [Header("Player UI Settings")]
    [SerializeField] private RectTransform playerUIPanel;
    [SerializeField] private Image p1PanelBg, p2PanelBg;
    [SerializeField] private TMPro.TextMeshProUGUI messageText;

    [Header("P1 Info")]
    [SerializeField] private TMPro.TextMeshProUGUI p1NameText;
    [SerializeField] private TMPro.TextMeshProUGUI p1MovesText;
    [SerializeField] private TMPro.TextMeshProUGUI p1HpText;
    [SerializeField] private Image p1HpBar;
    [SerializeField] private Image p1HpBarTrailing;

    [Header("P1 Pokemon 1")]
    [SerializeField] private Image p1Poke1Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1Name;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1EnergyText;
    [SerializeField] private Image p1Poke1EnergyBar;
    [SerializeField] private Image p1Poke1Stone;
    [SerializeField] private Transform p1Poke1EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1AttackLabel;

    [Header("P1 Pokemon 2")]
    [SerializeField] private Image p1Poke2Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke2Name;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke2EnergyText;
    [SerializeField] private Image p1Poke2EnergyBar;
    [SerializeField] private Image p1Poke2Stone;
    [SerializeField] private Transform p1Poke2EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke2AttackLabel;

    [Header("P2 Info")]
    [SerializeField] private TMPro.TextMeshProUGUI p2NameText;
    [SerializeField] private TMPro.TextMeshProUGUI p2MovesText;
    [SerializeField] private TMPro.TextMeshProUGUI p2HpText;
    [SerializeField] private Image p2HpBar;
    [SerializeField] private Image p2HpBarTrailing;

    [Header("P1 Evolution")]
    [SerializeField] private Image p1EvoStoneIcon;
    [SerializeField] private TMPro.TextMeshProUGUI p1EvoStoneText;
    [SerializeField] private Image p1EvoGlow;   // optional glow ring shown when evolved

    [Header("P2 Pokemon 1")]
    [SerializeField] private Image p2Poke1Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1Name;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1EnergyText;
    [SerializeField] private Image p2Poke1EnergyBar;
    [SerializeField] private Image p2Poke1Stone;
    [SerializeField] private Transform p2Poke1EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1AttackLabel;

    [Header("P2 Pokemon 2")]
    [SerializeField] private Image p2Poke2Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke2Name;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke2EnergyText;
    [SerializeField] private Image p2Poke2EnergyBar;
    [SerializeField] private Image p2Poke2Stone;
    [SerializeField] private Transform p2Poke2EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke2AttackLabel;

    [Header("P2 Evolution")]
    [SerializeField] private Image p2EvoStoneIcon;
    [SerializeField] private TMPro.TextMeshProUGUI p2EvoStoneText;
    [SerializeField] private Image p2EvoGlow;   // optional glow ring shown when evolved

    // --- Pip charge-bar system ---
    // Layout: index = playerIdx * 2 + pokemonIdx  (0=P1/Poke1, 1=P1/Poke2, 2=P2/Poke1, 3=P2/Poke2)
    private Image[][] pokemonPips          = new Image[4][];
    private TMPro.TextMeshProUGUI[] pokemonAttackLabels = new TMPro.TextMeshProUGUI[4];
    private Transform[] pokemonEnergyBgTransforms       = new Transform[4];
    private int _pokemonRowIndex; // incremented inside CreatePokemonRow

    private Vector3 _originalMessagePos;
    private float _displayedP1HP = -1f;
    private float _displayedP2HP = -1f;

    private GameObject _gameOverPopupInstance;
    private GameObject _evoSelectionPopupInstance;
    private GameObject _evoSuccessPopupInstance;

    private void Start()
    {
        // ── Safe-area canvas adjustment ────────────────────────────────────────
        // Shrinks the root Canvas RectTransform to fit inside the device safe area
        // so UI is never hidden behind notches, camera punch-outs, or home bars.
        ApplySafeArea();

        if (messageText != null)
        {
            _originalMessagePos = messageText.transform.localPosition;
            messageText.alpha = 0f;
        }

        // Allow sprites to be assigned in the inspector. If any slots are empty,
        // attempt to fill them from Resources/Gems/element_block_1..6.
        int expected = 7;
        if (gemSprites == null || gemSprites.Length != expected)
            gemSprites = new Sprite[expected];

        bool anyLoaded = false;
        for (int i = 0; i < 6; i++)
        {
            if (gemSprites[i] == null)
                gemSprites[i] = Resources.Load<Sprite>("Gems/element_block_" + (i + 1));
            if (gemSprites[i] != null)
                anyLoaded = true;
        }

        if (gemSprites[6] == null)
            gemSprites[6] = Resources.Load<Sprite>("Gems/evolution_stone");
        if (gemSprites[6] != null)
            anyLoaded = true;

        if (!anyLoaded)
        {
            UnityEngine.Debug.LogWarning("[BoardInputHandler] No gem sprites found. " +
                "Place gem sprites in Assets/Resources/Gems/ named element_block_1..element_block_6 " +
                "and evolution_stone, or assign the `gemSprites` array in the inspector.");
        }

        for (int i = 0; i < expected; i++)
        {
            if (gemSprites[i] == null)
            {
                if (fallbackGemSprite == null)
                {
                    Texture2D tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    fallbackGemSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                }
                gemSprites[i] = fallbackGemSprite;
            }
        }

        BoardManager board = BoardManager.GetInstance();
        if (board == null)
        {
            GameObject go = new GameObject("BoardManager");
            board = go.AddComponent<BoardManager>();
        }

        board.InitBoard();
        ResetInactivityTimer();
    }

    /// <summary>
    /// Adjusts the root Canvas RectTransform to sit inside the device safe area.
    /// This prevents UI elements from being obscured by notches or home indicators.
    /// </summary>
    private void ApplySafeArea()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        RectTransform canvasRt = rootCanvas.GetComponent<RectTransform>();
        if (canvasRt == null) return;

        Rect safe   = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        Vector2 anchorMin = safe.position / screenSize;
        Vector2 anchorMax = (safe.position + safe.size) / screenSize;

        canvasRt.anchorMin = anchorMin;
        canvasRt.anchorMax = anchorMax;
        canvasRt.offsetMin = Vector2.zero;
        canvasRt.offsetMax = Vector2.zero;
    }

    /// <summary>Resolves the cell size to use for the current device screen.</summary>
    private float ResolveCellSize()
    {
        // If AspectRatio has run, use its dynamically computed max board cell size.
        // Otherwise fall back to the inspector value.
        float dynamic = AspectRatio.MaxBoardCellSize;
        return (dynamic > 0f) ? dynamic : cellSize;
    }

    private void Update()
    {
        if (!isInitialized) return;

        BoardManager board = BoardManager.GetInstance();

        // Safety net: if the board has been "processing" for over 10 seconds,
        // something went wrong — force-release the lock so the game isn't stuck.
        if (board != null && board.IsProcessing)
        {
            _processingWatchdog += Time.deltaTime;
            if (_processingWatchdog > 10f)
            {
                UnityEngine.Debug.LogWarning("[BoardInputHandler] IsProcessing watchdog fired — forcing release.");
                board.IsProcessing = false;
                _processingWatchdog = 0f;
            }
        }
        else
        {
            _processingWatchdog = 0f;
        }

        if (isAnimating)
        {
            ResetInactivityTimer();
            return;
        }
        if (board != null && board.IsProcessing)
        {
            ResetInactivityTimer();
            return;
        }

        inactivityTime += Time.deltaTime;

        if (inactivityTime >= 3.0f && !hintShowing)
        {
            ShowHint();
        }
    }

    public void ResetInactivityTimer()
    {
        inactivityTime = 0f;
        if (hintShowing)
        {
            StopHint();
        }
    }

    private void ShowHint()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board.Grid == null) return;

        Vector2Int from, to;
        if (board.Grid.FindPossibleMove(out from, out to))
        {
            hintShowing = true;

            hintSequence = DOTween.Sequence();
            hintSequence.Join(gemRects[from.y, from.x].DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo));
            hintSequence.Join(gemRects[to.y, to.x].DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo));
        }
    }

    private void StopHint()
    {
        hintShowing = false;
        if (hintSequence != null)
        {
            hintSequence.Kill();
            hintSequence = null;
        }

        BoardManager board = BoardManager.GetInstance();
        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                if (gemRects != null && gemRects[r, c] != null)
                {
                    // Only restore scale for visible cells — Charry (hidden) cells
                    // must stay at scale 0 so they don’t flash back into view.
                    bool isCharry = board?.Grid != null
                                 && board.Grid.Grid[r, c] == GemType.Charry;
                    if (!isCharry)
                        gemRects[r, c].localScale = Vector3.one;
                }
            }
        }
    }

    private void OnBoardInit()
    {
        // ── Stop all in-flight coroutines and tweens BEFORE destroying objects ──
        // AnimateCascade and other coroutines run on THIS MonoBehaviour.
        // If we destroy gem GameObjects while they are still being tweened,
        // DOTween fires callbacks on destroyed Images → MissingReferenceException.
        StopAllCoroutines();
        isAnimating = false;

        // Kill every tween linked to existing gem GOs before Destroy() is called.
        if (gemImages != null)
        {
            for (int r = 0; r < GridModel.ROWS; r++)
                for (int c = 0; c < GridModel.COLS; c++)
                    if (gemImages[r, c] != null && gemImages[r, c])
                        DOTween.Kill(gemImages[r, c].gameObject, complete: false);
        }

        // Null-out the arrays so stale references can't escape into callbacks
        // that fire between Destroy() and the next frame's GC.
        gemImages = null;
        gemRects  = null;

        // Clear the coord map — old gem InstanceIDs are invalid after Destroy()
        _gemCoordMap.Clear();

        if (boardParent != null)
        {
            for (int i = boardParent.childCount - 1; i >= 0; i--)
                Destroy(boardParent.GetChild(i).gameObject);
        }
        _boardBlurOverlay = null;

        // Resolve the dynamic cell size for the current device BEFORE building the grid.
        _resolvedCellSize = ResolveCellSize();

        // Reset pip tracking arrays so InitPips rebuilds them cleanly.
        pokemonPips               = new Image[4][];
        pokemonAttackLabels       = new TMPro.TextMeshProUGUI[4];
        pokemonEnergyBgTransforms = new Transform[4];
        // ────────────────────────────────────────────────────────────────────────

        CreateGrid();
        CreatePlayerUI();
        RefreshBoard();
        InitPips();   // build segmented pip bars now that Pokémon types are known
        isInitialized = true;
        UpdateBoardBlur();
    }

    private static bool IsAlive(Image img)   => img != null && img;
    private static bool IsAlive(RectTransform rt) => rt != null && rt;
    private static bool IsAlive(TMPro.TextMeshProUGUI txt) => txt != null && txt;


    private void CreateGrid()
    {
        gemImages = new Image[GridModel.ROWS, GridModel.COLS];
        gemRects = new RectTransform[GridModel.ROWS, GridModel.COLS];

        // Use the device-adaptive cell size resolved in OnBoardInit.
        float cs     = _resolvedCellSize;
        float totalW = GridModel.COLS * cs + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cs + (GridModel.ROWS - 1) * spacing;

        boardRect = boardParent.GetComponent<RectTransform>();
        boardRect.sizeDelta = new Vector2(totalW, totalH);

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                GameObject cell = new GameObject("Gem");
                cell.transform.SetParent(boardParent, false);
                RectTransform rt = cell.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cs, cs);
                rt.anchoredPosition = new Vector2(
                    c * (cs + spacing) - totalW / 2f + cs / 2f,
                    -(r * (cs + spacing) - totalH / 2f + cs / 2f)
                );

                GameObject spriteObj = new GameObject("Sprite", typeof(Image));
                spriteObj.transform.SetParent(cell.transform, false);
                RectTransform spriteRt = spriteObj.GetComponent<RectTransform>();
                spriteRt.anchorMin = Vector2.zero;
                spriteRt.anchorMax = Vector2.one;
                spriteRt.sizeDelta = Vector2.zero;
                spriteRt.localScale = Vector3.one;

                Image img = spriteObj.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = true;

                // NOTE: Outline components are deliberately NOT added here.
                // Unity UI Outline/Shadow causes a full Canvas rebuild on every
                // frame the gem moves, which is the #1 cause of gameplay stutter.
                // Visual separation is handled by the board background color instead.

                gemImages[r, c] = img;
                gemRects[r, c] = rt;

                // O(1) lookup map: InstanceID → grid coords
                _gemCoordMap[cell.GetInstanceID()] = new Vector2Int(c, r);

                EventTrigger trigger = cell.AddComponent<EventTrigger>();

                EventTrigger.Entry clickEntry = new EventTrigger.Entry();
                clickEntry.eventID = EventTriggerType.PointerClick;
                clickEntry.callback.AddListener((data) => OnGemClick(cell));
                trigger.triggers.Add(clickEntry);

                EventTrigger.Entry beginEntry = new EventTrigger.Entry();
                beginEntry.eventID = EventTriggerType.BeginDrag;
                beginEntry.callback.AddListener((data) => OnGemDragBegin(cell));
                trigger.triggers.Add(beginEntry);

                EventTrigger.Entry dragEntry = new EventTrigger.Entry();
                dragEntry.eventID = EventTriggerType.Drag;
                dragEntry.callback.AddListener((data) => OnGemDrag((PointerEventData)data, cell));
                trigger.triggers.Add(dragEntry);

                EventTrigger.Entry endEntry = new EventTrigger.Entry();
                endEntry.eventID = EventTriggerType.EndDrag;
                endEntry.callback.AddListener((data) => OnGemDragEnd((PointerEventData)data));
                trigger.triggers.Add(endEntry);
            }
        }
    }

    private Vector2Int? GetGemCoords(GameObject go)
    {
        // O(1) lookup via pre-built instance-ID map (replaces the O(64) linear scan)
        if (_gemCoordMap.TryGetValue(go.GetInstanceID(), out Vector2Int coords))
            return coords;
        return null;
    }

    private void OnGemDragBegin(GameObject cell)
    {
        if (!isInitialized) return;
        if (isAnimating) return;
        if (BoardManager.GetInstance().IsProcessing) return;

        ResetInactivityTimer();

        Vector2Int? coords = GetGemCoords(cell);
        if (coords == null) return;
        int row = coords.Value.y;
        int col = coords.Value.x;

        if (firstSelection != null)
        {
            gemRects[firstSelection.Value.y, firstSelection.Value.x].DOScale(1f, 0.1f);
        }

        firstSelection = new Vector2Int(col, row);
        gemRects[row, col].DOScale(1.15f, 0.15f).SetEase(Ease.OutBack);
    }

    private void OnGemDrag(PointerEventData data, GameObject cell)
    {
        if (!isInitialized || isAnimating || _dragCommitted) return;
        if (BoardManager.GetInstance().IsProcessing) return;
        if (firstSelection == null) return;

        ResetInactivityTimer();

        Vector2Int? coords = GetGemCoords(cell);
        if (coords == null) return;
        int row = coords.Value.y;
        int col = coords.Value.x;

        // Ensure drag is from the selected gem
        if (firstSelection.Value.x != col || firstSelection.Value.y != row) return;

        Vector2 delta = data.position - data.pressPosition;
        if (delta.magnitude < 30f) return; // reduced threshold — feels snappier

        Vector2Int from = firstSelection.Value;
        Vector2Int to = from;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            to.x += delta.x > 0 ? 1 : -1;
        else
            to.y += delta.y > 0 ? 1 : -1;

        if (!GridModel.IsValidPosition(to.y, to.x) || to == from) return;

        gemRects[from.y, from.x].DOScale(1f, 0.1f);
        firstSelection = null;
        _dragCommitted = true; // prevent re-entry for this drag gesture

        StartCoroutine(AnimateSwap(from, to));
    }

    private void OnGemDragEnd(PointerEventData data)
    {
        _dragCommitted = false; // reset for next gesture
        if (!isInitialized) return;
        if (isAnimating) return;
        if (BoardManager.GetInstance().IsProcessing) return;
        if (firstSelection == null) return;

        Vector2Int from = firstSelection.Value;
        firstSelection = null;
        gemRects[from.y, from.x].DOScale(1f, 0.1f);
    }

    private void OnGemClick(GameObject cell)
    {
        if (!isInitialized) return;
        if (isAnimating) return;
        if (BoardManager.GetInstance().IsProcessing) return;

        ResetInactivityTimer();

        Vector2Int? coords = GetGemCoords(cell);
        if (coords == null) return;
        int row = coords.Value.y;
        int col = coords.Value.x;

        Vector2Int pos = new Vector2Int(col, row);

        if (firstSelection == null)
        {
            firstSelection = pos;
            gemRects[row, col].DOScale(1.15f, 0.15f).SetEase(Ease.OutBack);
        }
        else
        {
            Vector2Int from = firstSelection.Value;
            gemRects[from.y, from.x].DOScale(1f, 0.1f);

            if (!GridModel.AreAdjacent(from, pos) || from == pos)
            {
                firstSelection = pos;
                gemRects[row, col].DOScale(1.15f, 0.15f).SetEase(Ease.OutBack);
                return;
            }

            firstSelection = null;
            StartCoroutine(AnimateSwap(from, pos));
        }
    }

    /// <summary>
    /// Swaps the entries for two cells in both the gemImages/gemRects arrays
    /// AND the _gemCoordMap so future drag/click lookups resolve to the correct coords.
    /// </summary>
    private void SwapCellData(Vector2Int a, Vector2Int b)
    {
        // Update gemImages
        Image tmpImg = gemImages[a.y, a.x];
        gemImages[a.y, a.x] = gemImages[b.y, b.x];
        gemImages[b.y, b.x] = tmpImg;

        // Update gemRects
        RectTransform tmpRt = gemRects[a.y, a.x];
        gemRects[a.y, a.x] = gemRects[b.y, b.x];
        gemRects[b.y, b.x] = tmpRt;

        // *** Critical: keep the lookup map in sync ***
        // After the swap, gemRects[a] now holds what was at b, and vice-versa.
        // Update their InstanceID entries to the new coords.
        if (gemRects[a.y, a.x] != null)
            _gemCoordMap[gemRects[a.y, a.x].gameObject.GetInstanceID()] = a;
        if (gemRects[b.y, b.x] != null)
            _gemCoordMap[gemRects[b.y, b.x].gameObject.GetInstanceID()] = b;
    }

    private IEnumerator AnimateSwap(Vector2Int from, Vector2Int to)
    {
        isAnimating = true;
        BoardManager board = BoardManager.GetInstance();

        RectTransform rtA = gemRects[from.y, from.x];
        RectTransform rtB = gemRects[to.y, to.x];
        Vector2 posA = rtA.anchoredPosition;
        Vector2 posB = rtB.anchoredPosition;

        rtA.DOAnchorPos(posB, 0.2f).SetEase(Ease.InOutQuad);
        rtB.DOAnchorPos(posA, 0.2f).SetEase(Ease.InOutQuad);
        yield return _waitSwap;

        // Swap arrays + keep _gemCoordMap up-to-date
        SwapCellData(from, to);

        bool valid = board.TrySwap(from, to);

        if (!valid)
        {
            // Revert visual positions
            gemRects[to.y, to.x].DOAnchorPos(posA, 0.2f).SetEase(Ease.InOutQuad);
            gemRects[from.y, from.x].DOAnchorPos(posB, 0.2f).SetEase(Ease.InOutQuad);
            yield return _waitSwap;

            // Revert arrays + coord map
            SwapCellData(from, to);

            rtA.anchoredPosition = posA;
            rtB.anchoredPosition = posB;

            gemRects[to.y, to.x].DOShakePosition(0.3f, 5f);
        }

        isAnimating = false;
    }

    public void ExecuteBotSwap(Vector2Int from, Vector2Int to)
    {
        if (!isInitialized) return;
        if (isAnimating) return;
        if (BoardManager.GetInstance().IsProcessing) return;

        StartCoroutine(AnimateSwap(from, to));
    }

    private void OnMatchesFound(List<Vector2Int> matches)
    {
        if (gemImages == null || gemRects == null) return;

        foreach (Vector2Int pos in matches)
        {
            if (pos.y < 0 || pos.y >= GridModel.ROWS || pos.x < 0 || pos.x >= GridModel.COLS) continue;

            RectTransform rt  = gemRects[pos.y, pos.x];
            Image          img = gemImages[pos.y, pos.x];
            if (!IsAlive(img) || !IsAlive(rt)) continue;

            // Kill any existing tween on this GO.
            DOTween.Kill(img.gameObject, complete: false);

            // Ensure a CanvasGroup exists for fading.
            var cg = img.GetComponent<CanvasGroup>();
            if (cg == null) cg = img.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            img.raycastTarget = false;
            rt.localScale = Vector3.one;

            // Pop → shrink → fade
            Sequence seq = DOTween.Sequence();
            seq.Append(rt.DOScale(1.25f, 0.1f).SetEase(Ease.OutBack));
            seq.Append(rt.DOScale(0f,    0.2f).SetEase(Ease.InBack));
            seq.Join(cg.DOFade(0f, 0.25f).SetDelay(0.05f));
            seq.SetLink(img.gameObject);
        }
    }

    private void OnCascadeComplete(List<Vector2Int> newStonePositions, List<CascadeMove> cascadeMoves)
    {
        StartCoroutine(AnimateCascade(newStonePositions, cascadeMoves));
    }

    private IEnumerator AnimateCascade(List<Vector2Int> newStonePositions, List<CascadeMove> cascadeMoves)
    {
        // Guard: arrays may have been cleared by OnBoardInit during a game reset.
        if (gemImages == null || gemRects == null) yield break;

        HashSet<Vector2Int> newSet = new HashSet<Vector2Int>(newStonePositions);

        // Build a lookup: for each position that an existing stone fell TO,
        // store where it came FROM so we can animate the fall.
        Dictionary<Vector2Int, Vector2Int> fallFromMap = new Dictionary<Vector2Int, Vector2Int>();
        foreach (CascadeMove move in cascadeMoves)
        {
            if (!fallFromMap.ContainsKey(move.to))
                fallFromMap[move.to] = move.from;
        }
        HashSet<Vector2Int> fallToSet = new HashSet<Vector2Int>(fallFromMap.Keys);

        BoardManager board = BoardManager.GetInstance();
        if (board?.Grid == null) yield break;

        float totalW = GridModel.COLS * _resolvedCellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * _resolvedCellSize + (GridModel.ROWS - 1) * spacing;
        float dropDistance = _resolvedCellSize * 2.0f;

        // Helper to convert grid coords to anchored position
        Vector2 GridToAnchored(int row, int col)
        {
            return new Vector2(
                col * (_resolvedCellSize + spacing) - totalW / 2f + _resolvedCellSize / 2f,
                -(row * (_resolvedCellSize + spacing) - totalH / 2f + _resolvedCellSize / 2f)
            );
        }

        // ── PASS 1: hard-reset every cell synchronously ─────────────────────────
        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                // Re-check every iteration — board reset can clear arrays mid-loop
                if (gemImages == null || gemRects == null) yield break;

                Image         img = gemImages[r, c];
                RectTransform rt2 = gemRects[r, c];
                if (!IsAlive(img) || !IsAlive(rt2)) continue;

                GemType gem = board.Grid.Grid[r, c];
                int     idx = (int)gem;

                DOTween.Kill(img.gameObject, complete: false);
                rt2.DOKill(complete: false);
                var cg = img.GetComponent<CanvasGroup>();
                if (cg != null) { cg.DOKill(complete: false); cg.alpha = 1f; }

                Vector2 targetPos = GridToAnchored(r, c);

                bool isCharry  = idx == (int)GemType.Charry;
                bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
                bool isFalling = fallToSet.Contains(new Vector2Int(c, r));
                bool isNew     = !isFalling && newSet.Contains(new Vector2Int(c, r));

                if (isCharry)
                {
                    img.sprite = null; img.color = Color.clear;
                    if (cg != null) cg.alpha = 0f;
                    rt2.anchoredPosition = targetPos;
                    rt2.localScale = Vector3.zero;
                    img.raycastTarget = false;
                    img.transform.localScale = Vector3.one;
                }
                else if (isFalling)
                {
                    // Existing stone that fell during cascade — position at OLD
                    // position so PASS 2 can animate it falling to the new position.
                    Vector2Int oldPos = fallFromMap[new Vector2Int(c, r)];
                    Vector2 oldAnchored = GridToAnchored(oldPos.y, oldPos.x);

                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    if (cg != null) cg.alpha = 1f;
                    rt2.anchoredPosition = oldAnchored;
                    rt2.localScale = Vector3.one;
                    img.raycastTarget = false;
                    img.transform.localScale = (gem == GemType.Evolution) ? new Vector3(1.35f, 1.35f, 1f) : Vector3.one;

                    // Bring this cell to front so it renders above other cells
                    // during the falling animation (avoids overlap artifacts).
                    rt2.SetAsLastSibling();
                }
                else if (isNew)
                {
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    if (cg != null) cg.alpha = 0f;
                    rt2.anchoredPosition = targetPos + new Vector2(0f, dropDistance);
                    rt2.localScale = Vector3.one * 0.6f;
                    img.raycastTarget = false;
                    img.transform.localScale = (gem == GemType.Evolution) ? new Vector3(1.35f, 1.35f, 1f) : Vector3.one;
                }
                else
                {
                    // Stationary existing stone — already at the right position.
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    if (cg != null) cg.alpha = 1f;
                    rt2.anchoredPosition = targetPos;
                    rt2.localScale = Vector3.one;
                    img.raycastTarget = true;
                    img.transform.localScale = (gem == GemType.Evolution) ? new Vector3(1.35f, 1.35f, 1f) : Vector3.one;
                }
            }
        }

        yield return null; // let Pass-1 render before tweens start

        // ── PASS 2: guard again — board may have reset during the yield ──────────
        if (gemImages == null || gemRects == null) yield break;

        // Animate falling stones AND new stones dropping in
        HashSet<Vector2Int> fallingAnimated = new HashSet<Vector2Int>();

        // First, animate existing stones that fell (cascade moves)
        foreach (CascadeMove move in cascadeMoves)
        {
            if (gemImages == null || gemRects == null) yield break;

            // Skip if this cell was already animated (duplicate to-position from
            // ability-triggered cascades invalidating earlier cascade moves).
            if (fallingAnimated.Contains(move.to)) continue;

            int r = move.to.y;
            int c = move.to.x;

            Image         img2 = gemImages[r, c];
            RectTransform rt2b = gemRects[r, c];
            if (!IsAlive(img2) || !IsAlive(rt2b)) continue;

            GemType gem2 = board.Grid.Grid[r, c];
            if (gem2 == GemType.Charry) continue;

            Vector2 targetPos2 = GridToAnchored(r, c);

            var cg2 = img2.GetComponent<CanvasGroup>();
            float delay = c * 0.03f;

            rt2b.DOAnchorPos(targetPos2, 0.30f)
                .SetEase(Ease.OutBounce)
                .SetDelay(delay)
                .SetLink(img2.gameObject);

            rt2b.DOScale(1f, 0.22f)
                .SetEase(Ease.OutBack)
                .SetDelay(delay)
                .SetLink(img2.gameObject);

            if (cg2 != null)
                cg2.DOFade(1f, 0.20f)
                    .SetDelay(delay)
                    .SetLink(img2.gameObject);

            img2.raycastTarget = true;
            fallingAnimated.Add(move.to);
        }

        // Then, animate brand-new stones dropping in from the top
        foreach (Vector2Int cell in newStonePositions)
        {
            if (gemImages == null || gemRects == null) yield break;

            // Skip if already animated as a falling stone (shouldn't happen, but guard)
            if (fallingAnimated.Contains(cell)) continue;

            int r = cell.y;
            int c = cell.x;

            Image         img2 = gemImages[r, c];
            RectTransform rt2b = gemRects[r, c];
            if (!IsAlive(img2) || !IsAlive(rt2b)) continue;

            GemType gem2 = board.Grid.Grid[r, c];
            if (gem2 == GemType.Charry) continue;

            Vector2 targetPos2 = GridToAnchored(r, c);

            var cg2 = img2.GetComponent<CanvasGroup>();
            float delay = c * 0.03f;

            rt2b.DOAnchorPos(targetPos2, 0.30f)
                .SetEase(Ease.OutBounce)
                .SetDelay(delay)
                .SetLink(img2.gameObject);

            rt2b.DOScale(1f, 0.22f)
                .SetEase(Ease.OutBack)
                .SetDelay(delay)
                .SetLink(img2.gameObject);

            if (cg2 != null)
                cg2.DOFade(1f, 0.20f)
                    .SetDelay(delay)
                    .SetLink(img2.gameObject);

            img2.raycastTarget = true;
        }
    }

    private void RefreshBoard()
    {
        if (gemImages == null || gemRects == null) return;
        var bm = BoardManager.GetInstance();
        if (bm == null || bm.Grid == null) return;

        float totalW = GridModel.COLS * _resolvedCellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * _resolvedCellSize + (GridModel.ROWS - 1) * spacing;

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                Image         img = gemImages[r, c];
                RectTransform rt  = gemRects[r, c];
                if (!IsAlive(img) || !IsAlive(rt)) continue;

                GemType gem = bm.Grid.Grid[r, c];
                int     idx = (int)gem;
                bool isCharry = idx == (int)GemType.Charry;

                var canvasGroup = img.GetComponent<CanvasGroup>();

                rt.anchoredPosition = new Vector2(
                    c * (_resolvedCellSize + spacing) - totalW / 2f + _resolvedCellSize / 2f,
                    -(r * (_resolvedCellSize + spacing) - totalH / 2f + _resolvedCellSize / 2f)
                );

                if (isCharry)
                {
                    img.sprite = null;
                    img.color  = Color.clear;
                    if (canvasGroup != null) canvasGroup.alpha = 0f;
                    rt.localScale = Vector3.zero;
                    img.raycastTarget = false;
                    img.transform.localScale = Vector3.one;
                }
                else
                {
                    if (canvasGroup != null) canvasGroup.alpha = 1f;
                    rt.localScale = Vector3.one;

                    bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    img.raycastTarget = true;
                    img.transform.localScale = (gem == GemType.Evolution) ? new Vector3(1.35f, 1.35f, 1f) : Vector3.one;
                }
            }
        }
    }

    private void EnforceFilledImage(Image img)
    {
        if (img != null && img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    private void CreatePlayerUI()
    {
        pokemonAttackLabels[0] = p1Poke1AttackLabel;
        pokemonAttackLabels[1] = p1Poke2AttackLabel;
        pokemonAttackLabels[2] = p2Poke1AttackLabel;
        pokemonAttackLabels[3] = p2Poke2AttackLabel;

        pokemonEnergyBgTransforms[0] = p1Poke1EnergyBgTransform;
        pokemonEnergyBgTransforms[1] = p1Poke2EnergyBgTransform;
        pokemonEnergyBgTransforms[2] = p2Poke1EnergyBgTransform;
        pokemonEnergyBgTransforms[3] = p2Poke2EnergyBgTransform;

        EnforceFilledImage(p1HpBar);
        EnforceFilledImage(p1HpBarTrailing);
        EnforceFilledImage(p2HpBar);
        EnforceFilledImage(p2HpBarTrailing);
        EnforceFilledImage(p1Poke1EnergyBar);
        EnforceFilledImage(p1Poke2EnergyBar);
        EnforceFilledImage(p2Poke1EnergyBar);
        EnforceFilledImage(p2Poke2EnergyBar);

        SetupPokemonClickListeners();

        UpdatePlayerUI();
    }

    private void UpdatePlayerUI()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board == null || board.Players == null) return;

        // playerUIPanel (and everything inside it — HP bars, energy bars, text) gets
        // destroyed during board reset. ProcessMatches can still fire OnHPChanged after
        // Destroy() is called, so we must bail out here to avoid MissingReferenceException
        // on Image.fillAmount / TMPro.text access on destroyed objects.
        if (playerUIPanel == null || !playerUIPanel) return;

        if (p1PanelBg != null) CreateEvolutionUI(0, p1PanelBg.transform);
        if (p2PanelBg != null) CreateEvolutionUI(1, p2PanelBg.transform);
        AdjustUIPanelScale();

        // P1 Main Info
        p1NameText.text = board.Players[0].Name;
        if (p1MovesText != null)
        {
            RectTransform rt = p1MovesText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(185f, 115f);
                rt.sizeDelta = new Vector2(200f, rt.sizeDelta.y);
            }
            p1MovesText.text = "Moves: " + GetMovesIndicator(board.Players[0].MovesRemaining);
        }
        
        if (_displayedP1HP < 0) _displayedP1HP = board.Players[0].HP;
        float targetP1HP = board.Players[0].HP;
        
        if (Mathf.Abs(_displayedP1HP - targetP1HP) > 0.01f && targetP1HP < _displayedP1HP)
        {
            // Damage: Animate text down to match the trailing bar
            DOTween.Kill("P1HPText");
            DOTween.To(() => _displayedP1HP, x => {
                _displayedP1HP = x;
                if (p1HpText != null)
                    p1HpText.text = Mathf.RoundToInt(_displayedP1HP) + " / " + board.Players[0].MaxHP + (board.Players[0].Shield > 0 ? " [" + board.Players[0].Shield + " SHIELD]" : "");
            }, targetP1HP, 0.6f).SetId("P1HPText").SetEase(Ease.OutCubic).SetDelay(0.2f);
        }
        else
        {
            _displayedP1HP = targetP1HP;
            p1HpText.text = board.Players[0].HP + " / " + board.Players[0].MaxHP + (board.Players[0].Shield > 0 ? " [" + board.Players[0].Shield + " SHIELD]" : "");
        }
        if (IsAlive(p1HpBar) && IsAlive(p1HpBarTrailing))
        {
            float target = (float)board.Players[0].HP / board.Players[0].MaxHP;
            if (target > p1HpBar.fillAmount) 
            {
                // Healing: main bar animates up, trailing bar snaps
                p1HpBar.DOFillAmount(target, 0.4f).SetEase(Ease.OutQuad).SetLink(p1HpBar.gameObject);
                p1HpBarTrailing.DOKill();
                p1HpBarTrailing.fillAmount = target;
            }
            else if (target < p1HpBar.fillAmount)
            {
                // Damage: main bar snaps down, trailing bar smoothly catches up
                p1HpBar.DOKill();
                p1HpBar.fillAmount = target;
                p1HpBarTrailing.DOKill();
                p1HpBarTrailing.DOFillAmount(target, 0.6f).SetEase(Ease.OutCubic).SetDelay(0.2f).SetLink(p1HpBarTrailing.gameObject);
            }
        }

        // P1 Pokemon 1
        var p1Poke1 = board.Players[0].Pokemons[0];
        p1Poke1Avatar.sprite = p1Poke1.Avatar;
        p1Poke1Name.text = (p1Poke1.IsEvolved ? "★ Evolved " : "") + p1Poke1.Name + " (" + p1Poke1.Type + ")";
        p1Poke1EnergyText.text = "Energy: " + p1Poke1.CurrentEnergy + "/" + p1Poke1.MaxEnergy;
        if (p1Poke1EnergyBar != null && p1Poke1EnergyBar) p1Poke1EnergyBar.fillAmount = (float)p1Poke1.CurrentEnergy / p1Poke1.MaxEnergy;
        if (IsAlive(p1Poke1Stone))
        {
            int idx = (int)p1Poke1.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p1Poke1Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p1Poke1Stone.color = hasSprite ? Color.white : GetGemColor(p1Poke1.Type);
        }

        // P1 Pokemon 2
        var p1Poke2 = board.Players[0].Pokemons[1];
        p1Poke2Avatar.sprite = p1Poke2.Avatar;
        p1Poke2Name.text = (p1Poke2.IsEvolved ? "★ Evolved " : "") + p1Poke2.Name + " (" + p1Poke2.Type + ")";
        p1Poke2EnergyText.text = "Energy: " + p1Poke2.CurrentEnergy + "/" + p1Poke2.MaxEnergy;
        if (p1Poke2EnergyBar != null && p1Poke2EnergyBar) p1Poke2EnergyBar.fillAmount = (float)p1Poke2.CurrentEnergy / p1Poke2.MaxEnergy;
        if (IsAlive(p1Poke2Stone))
        {
            int idx = (int)p1Poke2.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p1Poke2Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p1Poke2Stone.color = hasSprite ? Color.white : GetGemColor(p1Poke2.Type);
        }

        // P2 Main Info
        p2NameText.text = board.Players[1].Name;
        if (p2MovesText != null)
        {
            RectTransform rt = p2MovesText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(185f, 115f);
                rt.sizeDelta = new Vector2(200f, rt.sizeDelta.y);
            }
            p2MovesText.text = "Moves: " + GetMovesIndicator(board.Players[1].MovesRemaining);
        }
        
        if (_displayedP2HP < 0) _displayedP2HP = board.Players[1].HP;
        float targetP2HP = board.Players[1].HP;
        
        if (Mathf.Abs(_displayedP2HP - targetP2HP) > 0.01f && targetP2HP < _displayedP2HP)
        {
            // Damage: Animate text down to match the trailing bar
            DOTween.Kill("P2HPText");
            DOTween.To(() => _displayedP2HP, x => {
                _displayedP2HP = x;
                if (p2HpText != null)
                    p2HpText.text = Mathf.RoundToInt(_displayedP2HP) + " / " + board.Players[1].MaxHP + (board.Players[1].Shield > 0 ? " [" + board.Players[1].Shield + " SHIELD]" : "");
            }, targetP2HP, 0.6f).SetId("P2HPText").SetEase(Ease.OutCubic).SetDelay(0.2f);
        }
        else
        {
            _displayedP2HP = targetP2HP;
            p2HpText.text = board.Players[1].HP + " / " + board.Players[1].MaxHP + (board.Players[1].Shield > 0 ? " [" + board.Players[1].Shield + " SHIELD]" : "");
        }
        if (IsAlive(p2HpBar) && IsAlive(p2HpBarTrailing))
        {
            float target = (float)board.Players[1].HP / board.Players[1].MaxHP;
            if (target > p2HpBar.fillAmount) 
            {
                // Healing: main bar animates up, trailing bar snaps
                p2HpBar.DOFillAmount(target, 0.4f).SetEase(Ease.OutQuad).SetLink(p2HpBar.gameObject);
                p2HpBarTrailing.DOKill();
                p2HpBarTrailing.fillAmount = target;
            }
            else if (target < p2HpBar.fillAmount)
            {
                // Damage: main bar snaps down, trailing bar smoothly catches up
                p2HpBar.DOKill();
                p2HpBar.fillAmount = target;
                p2HpBarTrailing.DOKill();
                p2HpBarTrailing.DOFillAmount(target, 0.6f).SetEase(Ease.OutCubic).SetDelay(0.2f).SetLink(p2HpBarTrailing.gameObject);
            }
        }

        // P2 Pokemon 1
        var p2Poke1 = board.Players[1].Pokemons[0];
        p2Poke1Avatar.sprite = p2Poke1.Avatar;
        p2Poke1Name.text = (p2Poke1.IsEvolved ? "★ Evolved " : "") + p2Poke1.Name + " (" + p2Poke1.Type + ")";
        p2Poke1EnergyText.text = "Energy: " + p2Poke1.CurrentEnergy + "/" + p2Poke1.MaxEnergy;
        if (p2Poke1EnergyBar != null && p2Poke1EnergyBar) p2Poke1EnergyBar.fillAmount = (float)p2Poke1.CurrentEnergy / p2Poke1.MaxEnergy;
        if (IsAlive(p2Poke1Stone))
        {
            int idx = (int)p2Poke1.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p2Poke1Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p2Poke1Stone.color = hasSprite ? Color.white : GetGemColor(p2Poke1.Type);
        }

        // P2 Pokemon 2
        var p2Poke2 = board.Players[1].Pokemons[1];
        p2Poke2Avatar.sprite = p2Poke2.Avatar;
        p2Poke2Name.text = (p2Poke2.IsEvolved ? "★ Evolved " : "") + p2Poke2.Name + " (" + p2Poke2.Type + ")";
        p2Poke2EnergyText.text = "Energy: " + p2Poke2.CurrentEnergy + "/" + p2Poke2.MaxEnergy;
        if (p2Poke2EnergyBar != null && p2Poke2EnergyBar) p2Poke2EnergyBar.fillAmount = (float)p2Poke2.CurrentEnergy / p2Poke2.MaxEnergy;
        if (IsAlive(p2Poke2Stone))
        {
            int idx = (int)p2Poke2.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p2Poke2Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p2Poke2Stone.color = hasSprite ? Color.white : GetGemColor(p2Poke2.Type);
        }

        // Highlight active turn with brighter translucent dark
        Color activeColor   = board.ActivePlayerIndex == 0 ? _activePanel   : _inactivePanel;
        Color inactiveColor = board.ActivePlayerIndex == 0 ? _inactivePanel : _activePanel;

        if (board.ActivePlayerIndex == 0)
        {
            p1PanelBg.DOColor(activeColor,   0.3f);
            p2PanelBg.DOColor(inactiveColor, 0.3f);
            p1PanelBg.transform.DOScale(1.03f, 0.3f);
            p2PanelBg.transform.DOScale(0.97f, 0.3f);
        }
        else
        {
            p1PanelBg.DOColor(inactiveColor, 0.3f);
            p2PanelBg.DOColor(activeColor,   0.3f);
            p1PanelBg.transform.DOScale(0.97f, 0.3f);
            p2PanelBg.transform.DOScale(1.03f, 0.3f);
        }

        // Evolution Stone UI updates
        if (IsAlive(p1EvoStoneIcon))
        {
            p1EvoStoneIcon.sprite = (gemSprites != null && gemSprites.Length > 6) ? gemSprites[6] : fallbackGemSprite;
        }
        if (IsAlive(p1EvoStoneText))
        {
            p1EvoStoneText.text = board.Players[0].EvolutionStones + "/" + PlayerState.EvolutionRequired;
        }
        if (IsAlive(p1EvoGlow))
        {
            bool anyEvolved = board.Players[0].Pokemons.Exists(p => p.IsEvolved);
            p1EvoGlow.gameObject.SetActive(anyEvolved);
        }

        if (IsAlive(p2EvoStoneIcon))
        {
            p2EvoStoneIcon.sprite = (gemSprites != null && gemSprites.Length > 6) ? gemSprites[6] : fallbackGemSprite;
        }
        if (IsAlive(p2EvoStoneText))
        {
            p2EvoStoneText.text = board.Players[1].EvolutionStones + "/" + PlayerState.EvolutionRequired;
        }
        if (IsAlive(p2EvoGlow))
        {
            bool anyEvolved = board.Players[1].Pokemons.Exists(p => p.IsEvolved);
            p2EvoGlow.gameObject.SetActive(anyEvolved);
        }

        RefreshPips();
    }

    private void UpdateBoardBlur()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board == null || boardParent == null) return;

        bool isBotTurn = (board.ActivePlayerIndex == 1);

        // CanvasGroup dimming + input blocking
        var cg = boardParent.GetComponent<CanvasGroup>();
        if (cg == null) cg = boardParent.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = isBotTurn ? 0.5f : 1f;
        cg.interactable = !isBotTurn;
        cg.blocksRaycasts = !isBotTurn;

        // Overlay to block interactions during bot turn
        if (isBotTurn)
        {
            if (_boardBlurOverlay == null)
            {
                _boardBlurOverlay = new GameObject("BoardBlurOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _boardBlurOverlay.transform.SetParent(boardParent, false);
                var rt = _boardBlurOverlay.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                
                var img = _boardBlurOverlay.GetComponent<Image>();
                img.color = new Color(0.05f, 0.05f, 0.1f, 0.6f); // dark blue-tinted overlay
            }
            _boardBlurOverlay.SetActive(true);
            _boardBlurOverlay.transform.SetAsLastSibling(); // ensure it is on top of gems
        }
        else
        {
            if (_boardBlurOverlay != null)
            {
                _boardBlurOverlay.SetActive(false);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Pip charge bar initialisation — called once after board + pokemon are set
    // -------------------------------------------------------------------------
    private void InitPips()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board?.Players == null) return;

        for (int p = 0; p < 2; p++)
        {
            for (int k = 0; k < board.Players[p].Pokemons.Count && k < 2; k++)
            {
                int idx = p * 2 + k;
                Transform bgTf = pokemonEnergyBgTransforms[idx];
                if (bgTf == null) continue;

                // Destroy only previously created pips (sparing the hidden EnergyFill)
                for (int ci = bgTf.childCount - 1; ci >= 0; ci--)
                {
                    GameObject child = bgTf.GetChild(ci).gameObject;
                    if (child.name.StartsWith("Pip_"))
                        Destroy(child);
                }

                PokemonState poke = board.Players[p].Pokemons[k];
                AttackRule rule   = board.GetAttackRule(poke.Type);
                int stonesRequired = poke.MaxEnergy;
                int n = stonesRequired; // Pokemon-specific energy limit (max 9)

                RectTransform bgRt = bgTf.GetComponent<RectTransform>();
                float barW   = bgRt.sizeDelta.x;
                float barH   = bgRt.sizeDelta.y;
                float gap    = 2f;
                float pipW   = (barW - (n - 1) * gap) / n;

                Image[] pips = new Image[n];
                for (int i = 0; i < n; i++)
                {
                    GameObject pipGo = new GameObject("Pip_" + i, typeof(RectTransform), typeof(Image));
                    pipGo.transform.SetParent(bgTf, false);
                    RectTransform pipRt = pipGo.GetComponent<RectTransform>();
                    pipRt.sizeDelta = new Vector2(pipW, barH);
                    float xPos = -barW / 2f + i * (pipW + gap) + pipW / 2f;
                    pipRt.anchoredPosition = new Vector2(xPos, 0f);
                    Image pipImg = pipGo.GetComponent<Image>();
                    pipImg.color = new Color(0.18f, 0.18f, 0.18f, 0.7f); // empty state
                    pips[i] = pipImg;
                }
                pokemonPips[idx] = pips;

                // Attack label: e.g. "Collect 6 Fire → Ember (10 dmg)"
                if (pokemonAttackLabels[idx] != null)
                {
                    string valUnit = (poke.Type == GemType.Nature || poke.Type == GemType.Healing) ? "heal" : "dmg";
                    int actualVal = poke.BaseValue + poke.EvolutionDamageBonus;
                    string dmgPart = $" ({actualVal} {valUnit})";
                    pokemonAttackLabels[idx].text =
                        $"Collect {n} {poke.Type} → {rule.AttackName}{dmgPart}";
                    pokemonAttackLabels[idx].color = GetGemColor(poke.Type) * 0.9f;
                }
            }
        }

        RefreshPips();
    }

    private void RefreshPips()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board?.Players == null) return;

        for (int p = 0; p < 2; p++)
        {
            for (int k = 0; k < board.Players[p].Pokemons.Count && k < 2; k++)
            {
                int idx      = p * 2 + k;
                Image[] pips = pokemonPips[idx];
                if (pips == null) continue;

                PokemonState poke = board.Players[p].Pokemons[k];
                Color gemCol   = GetGemColor(poke.Type); // uses static array — zero alloc

                int stonesRequired = poke.MaxEnergy;

                for (int i = 0; i < pips.Length; i++)
                {
                    // Guard destroyed pip Images (can happen during board reset)
                    if (!IsAlive(pips[i])) continue;

                    bool filled = i < poke.CurrentEnergy;
                    Color c = filled ? gemCol : _pipEmptyColor; // cached — no alloc
                    if (pips[i].color != c)
                        pips[i].color = c; // only dirty the canvas if the value changed
                    Vector3 scale = filled ? new Vector3(1f, 1.15f, 1f) : new Vector3(0.85f, 0.85f, 0.85f);
                    if (pips[i].transform.localScale != scale)
                        pips[i].transform.localScale = scale;
                }

                // Update the numeric energy label
                TMPro.TextMeshProUGUI label = (idx == 0) ? p1Poke1EnergyText
                                            : (idx == 1) ? p1Poke2EnergyText
                                            : (idx == 2) ? p2Poke1EnergyText
                                            :              p2Poke2EnergyText;
                if (label != null)
                    label.text = poke.CurrentEnergy + "/" + stonesRequired;
            }
        }
    }

    // Returns a vivid colour matching the gem type (uses pre-built array — zero alloc)
    private static Color GetGemColor(GemType type)
    {
        int idx = (int)type;
        return (idx >= 0 && idx < GemColors.Length) ? GemColors[idx] : Color.white;
    }

    // Pre-built move indicator strings — avoid string concat GC every moves event
    private static readonly string[] MovesIndicators = { "None", "●", "●●", "●●●" };

    private static string GetMovesIndicator(int count)
    {
        if (count <= 0) return "None";
        if (count < MovesIndicators.Length) return MovesIndicators[count];
        // Fallback for unusual counts
        return new string('●', count);
    }

    private void ShowMessage(string message)
    {
        // messageText is a child of MessageBanner; bail if destroyed.
        if (messageText == null || !messageText) return;
        
        // Reset base state
        messageText.transform.DOKill(complete: true);
        
        // Keep messageText perfectly centered inside the banner
        messageText.transform.localPosition = Vector3.zero;
        
        messageText.text = message;
        messageText.alpha = 0f;
        messageText.transform.localScale = Vector3.one * 1.5f;

        // Fade the banner background as well
        Transform banner = messageText.transform.parent;
        if (banner != null)
        {
            CanvasGroup bannerCg = banner.GetComponent<CanvasGroup>();
            if (bannerCg != null)
            {
                bannerCg.DOKill();
                bannerCg.alpha = 0f;
                bannerCg.DOFade(1f, 0.2f);
            }
        }

        Sequence seq = DOTween.Sequence();
        seq.Join(messageText.DOFade(1f, 0.2f));
        seq.Join(messageText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        
        // Add a gentle continuous floating effect without drifting away completely
        messageText.transform.DOLocalMoveY(4f, 1.5f)
            .SetRelative(true)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(messageText.gameObject);
        
        seq.SetLink(messageText.gameObject);
    }

    private void OnEnable()
    {
        BoardManager.OnBoardInitialized += OnBoardInit;
        BoardManager.OnMatchesFound     += OnMatchesFound;
        BoardManager.OnCascadeComplete  += OnCascadeComplete;

        BoardManager.OnTurnChanged   += UpdatePlayerUI;
        BoardManager.OnTurnChanged   += UpdateBoardBlur;
        BoardManager.OnMovesChanged  += UpdatePlayerUI;
        BoardManager.OnHPChanged     += UpdatePlayerUI;
        BoardManager.OnShowMessage   += ShowMessage;
        // Refresh pip bars whenever any Pokémon collects stones mid-turn
        BoardManager.OnEnergyChanged += RefreshPips;
        BoardManager.OnEvolutionStonesChanged += OnEvolutionUpdate;
        BoardManager.OnEvolved                 += OnEvolutionUpdate;
        BoardManager.OnGameOver                += ShowGameOverPopup;
        BoardManager.OnRequestEvolutionSelection += ShowEvolutionSelectionPopup;
        BoardManager.OnShowEvolutionSuccessPopup += ShowEvolutionSuccessPopup;
    }

    private void OnDisable()
    {
        BoardManager.OnBoardInitialized -= OnBoardInit;
        BoardManager.OnMatchesFound     -= OnMatchesFound;
        BoardManager.OnCascadeComplete  -= OnCascadeComplete;

        BoardManager.OnTurnChanged   -= UpdatePlayerUI;
        BoardManager.OnTurnChanged   -= UpdateBoardBlur;
        BoardManager.OnMovesChanged  -= UpdatePlayerUI;
        BoardManager.OnHPChanged     -= UpdatePlayerUI;
        BoardManager.OnShowMessage   -= ShowMessage;
        BoardManager.OnEnergyChanged -= RefreshPips;
        BoardManager.OnEvolutionStonesChanged -= OnEvolutionUpdate;
        BoardManager.OnEvolved                 -= OnEvolutionUpdate;
        BoardManager.OnGameOver                -= ShowGameOverPopup;
        BoardManager.OnRequestEvolutionSelection -= ShowEvolutionSelectionPopup;
        BoardManager.OnShowEvolutionSuccessPopup -= ShowEvolutionSuccessPopup;
    }

    private void CreateEvolutionUI(int playerIndex, Transform cardTransform)
    {
        // Check if already created or assigned
        if (playerIndex == 0 && p1EvoStoneIcon != null) return;
        if (playerIndex == 1 && p2EvoStoneIcon != null) return;

        // Create container GameObject
        GameObject container = new GameObject("EvolutionUI_Group", typeof(RectTransform));
        container.transform.SetParent(cardTransform, false);
        
        RectTransform containerRt = container.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.pivot = new Vector2(0.5f, 0.5f);
        containerRt.sizeDelta = new Vector2(100f, 40f);
        // Position it right in the middle between the two Pokemon columns
        containerRt.anchoredPosition = new Vector2(0f, -30f);

        // Create Image for Pokéball Icon
        GameObject iconGo = new GameObject("EvoStoneIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(container.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(30f, 30f);
        iconRt.anchoredPosition = new Vector2(5f, 0f);
        
        Image iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = (gemSprites != null && gemSprites.Length > 6) ? gemSprites[6] : fallbackGemSprite;
        iconImg.preserveAspect = true;

        // Create Text for Stone Count
        GameObject textGo = new GameObject("EvoStoneText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        textGo.transform.SetParent(container.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0.5f);
        textRt.anchorMax = new Vector2(1f, 0.5f);
        textRt.pivot = new Vector2(0f, 0.5f);
        textRt.offsetMin = new Vector2(40f, -15f);
        textRt.offsetMax = new Vector2(0f, 15f);

        TMPro.TextMeshProUGUI tmpText = textGo.GetComponent<TMPro.TextMeshProUGUI>();
        tmpText.fontSize = 16f;
        tmpText.alignment = TMPro.TextAlignmentOptions.Left;
        tmpText.font = messageText != null ? messageText.font : tmpText.font; // inherit font
        tmpText.color = Color.white;

        // Assign to fields
        if (playerIndex == 0)
        {
            p1EvoStoneIcon = iconImg;
            p1EvoStoneText = tmpText;
        }
        else
        {
            p2EvoStoneIcon = iconImg;
            p2EvoStoneText = tmpText;
        }
    }

    private void AdjustUIPanelScale()
    {
        if (playerUIPanel == null) return;
        
        // Find Canvas reference width
        Canvas rootCanvas = playerUIPanel.GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        RectTransform canvasRt = rootCanvas.GetComponent<RectTransform>();
        if (canvasRt == null) return;

        float canvasWidth = canvasRt.rect.width;
        // The default panel width is 840. Add a margin of 40px (880 total).
        if (canvasWidth < 880f && canvasWidth > 0f)
        {
            float targetScale = canvasWidth / 880f;
            playerUIPanel.localScale = new Vector3(targetScale, targetScale, 1f);
        }
        else
        {
            playerUIPanel.localScale = new Vector3(1.15f, 1.15f, 1f); // default scale is 1.15f in scene properties
        }
    }

    private void OnEvolutionUpdate(int playerIndex)
    {
        UpdatePlayerUI();
    }

    private void ShowGameOverPopup(int losingPlayerIdx)
    {
        if (_gameOverPopupInstance != null)
        {
            Destroy(_gameOverPopupInstance);
        }

        // Find canvas transform as root
        Canvas rootCanvas = boardParent != null ? boardParent.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        // 1. Create Overlay Panel (Background blocker)
        _gameOverPopupInstance = new GameObject("GameOverPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _gameOverPopupInstance.transform.SetParent(rootCanvas.transform, false);

        RectTransform overlayRt = _gameOverPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = _gameOverPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.75f); // Semi-transparent black blocker

        // 2. Create Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_gameOverPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(400f, 300f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        modalImg.color = new Color(0.12f, 0.12f, 0.16f, 1f); // Slate dark background

        // Add inner border
        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(modalWindow.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4f, 4f);
        borderRt.offsetMax = new Vector2(-4f, -4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.18f, 0.18f, 0.24f, 1f); // Dark inner body

        // 3. Create Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(borderGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(10f, -60f);
        titleRt.offsetMax = new Vector2(-10f, -10f);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = "GAME OVER";
        titleTxt.fontSize = 32f;
        titleTxt.fontStyle = TMPro.FontStyles.Bold;
        titleTxt.alignment = TMPro.TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.3f, 0.3f, 1f); // Soft Red
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Create Message Text (Winner / Loser details)
        GameObject msgGo = new GameObject("MessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        msgGo.transform.SetParent(borderGo.transform, false);
        RectTransform msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0f, 0.5f);
        msgRt.anchorMax = new Vector2(1f, 0.5f);
        msgRt.pivot = new Vector2(0.5f, 0.5f);
        msgRt.offsetMin = new Vector2(15f, -40f);
        msgRt.offsetMax = new Vector2(-15f, 40f);

        int winningPlayerIdx = losingPlayerIdx == 0 ? 1 : 0;
        string winnerName = BoardManager.GetInstance().Players[winningPlayerIdx].Name;
        string loserName = BoardManager.GetInstance().Players[losingPlayerIdx].Name;

        TMPro.TextMeshProUGUI msgTxt = msgGo.GetComponent<TMPro.TextMeshProUGUI>();
        msgTxt.text = $"<color=#00FF88>{winnerName} Wins!</color>\n<color=#FF4444>{loserName} Lost the Game!</color>";
        msgTxt.fontSize = 22f;
        msgTxt.lineSpacing = 1.2f;
        msgTxt.alignment = TMPro.TextAlignmentOptions.Center;
        msgTxt.color = Color.white;
        if (messageText != null) msgTxt.font = messageText.font;

        // 5. Create Button ("Play Again")
        GameObject btnGo = new GameObject("PlayAgainButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        btnGo.transform.SetParent(borderGo.transform, false);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.sizeDelta = new Vector2(180f, 45f);
        btnRt.anchoredPosition = new Vector2(0f, 25f);

        Image btnImg = btnGo.GetComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f, 1f); // Bright blue button

        // Button text
        GameObject btnTextGo = new GameObject("ButtonText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        btnTextGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTextRt = btnTextGo.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI btnTxt = btnTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        btnTxt.text = "Play Again";
        btnTxt.fontSize = 18f;
        btnTxt.fontStyle = TMPro.FontStyles.Bold;
        btnTxt.alignment = TMPro.TextAlignmentOptions.Center;
        btnTxt.color = Color.white;
        if (messageText != null) btnTxt.font = messageText.font;

        // Set up click action
        UnityEngine.UI.Button button = btnGo.GetComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(() =>
        {
            Destroy(_gameOverPopupInstance);
            BoardManager.GetInstance().InitBoard();
        });

        // Small bounce animation to the popup card
        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    private void ShowEvolutionSelectionPopup(int playerIdx, Action<PokemonState> onSelected)
    {
        if (playerIdx == 1)
        {
            PlayerState botPlayer = BoardManager.GetInstance().Players[1];
            PokemonState chosen = null;
            foreach (var p in botPlayer.Pokemons)
            {
                if (!p.IsEvolved)
                {
                    chosen = p;
                    break;
                }
            }
            if (chosen == null && botPlayer.Pokemons.Count > 0)
            {
                chosen = botPlayer.Pokemons[UnityEngine.Random.Range(0, botPlayer.Pokemons.Count)];
            }

            if (chosen != null)
            {
                onSelected?.Invoke(chosen);
            }
            return;
        }

        if (_evoSelectionPopupInstance != null)
        {
            Destroy(_evoSelectionPopupInstance);
        }

        Canvas rootCanvas = boardParent != null ? boardParent.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        PlayerState player = BoardManager.GetInstance().Players[playerIdx];

        // 1. Blocker Overlay
        _evoSelectionPopupInstance = new GameObject("EvoSelectionPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _evoSelectionPopupInstance.transform.SetParent(rootCanvas.transform, false);

        RectTransform overlayRt = _evoSelectionPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = _evoSelectionPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        // 2. Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_evoSelectionPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(500f, 430f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        modalImg.color = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark purple-grey

        // Inner Border
        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(modalWindow.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4f, 4f);
        borderRt.offsetMax = new Vector2(-4f, -4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(borderGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(10f, -60f);
        titleRt.offsetMax = new Vector2(-10f, -15f);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = "CHOOSE POKEMON TO EVOLVE";
        titleTxt.fontSize = 24f;
        titleTxt.fontStyle = TMPro.FontStyles.Bold;
        titleTxt.alignment = TMPro.TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.8f, 0.2f, 1f); // Gold title
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Create columns for the two Pokémon
        float cardWidth = 200f;
        float cardHeight = 260f;
        float spacing = 20f;

        for (int i = 0; i < player.Pokemons.Count && i < 2; i++)
        {
            PokemonState poke = player.Pokemons[i];
            
            // Create Column Panel (Button)
            GameObject cardGo = new GameObject($"PokeCard_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            cardGo.transform.SetParent(borderGo.transform, false);
            
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
            
            float xOffset = (i == 0) ? -(cardWidth / 2f + spacing / 2f) : (cardWidth / 2f + spacing / 2f);
            cardRt.anchoredPosition = new Vector2(xOffset, 10f);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.2f, 0.2f, 0.28f, 1f); // Dark card body

            // Outer outline for the pokemon card
            GameObject cardOutlineGo = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardOutlineGo.transform.SetParent(cardGo.transform, false);
            RectTransform outlineRt = cardOutlineGo.GetComponent<RectTransform>();
            outlineRt.anchorMin = Vector2.zero;
            outlineRt.anchorMax = Vector2.one;
            outlineRt.offsetMin = new Vector2(2f, 2f);
            outlineRt.offsetMax = new Vector2(-2f, -2f);
            Image outlineImg = cardOutlineGo.GetComponent<Image>();
            outlineImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

            // Pokémon Avatar/Sprite
            GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            avatarGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform avatarRt = avatarGo.GetComponent<RectTransform>();
            avatarRt.anchorMin = new Vector2(0.5f, 1f);
            avatarRt.anchorMax = new Vector2(0.5f, 1f);
            avatarRt.pivot = new Vector2(0.5f, 1f);
            avatarRt.sizeDelta = new Vector2(90f, 90f);
            avatarRt.anchoredPosition = new Vector2(0f, -15f);

            Image avatarImg = avatarGo.GetComponent<Image>();
            avatarImg.sprite = poke.Avatar;
            avatarImg.preserveAspect = true;

            // Pokémon Name
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.offsetMin = new Vector2(5f, 130f);
            nameRt.offsetMax = new Vector2(-5f, 160f);

            TMPro.TextMeshProUGUI nameTxt = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
            nameTxt.text = poke.Name;
            nameTxt.fontSize = 20f;
            nameTxt.fontStyle = TMPro.FontStyles.Bold;
            nameTxt.alignment = TMPro.TextAlignmentOptions.Center;
            nameTxt.color = Color.white;
            if (messageText != null) nameTxt.font = messageText.font;

            // Pokémon Stats Description
            GameObject statGo = new GameObject("StatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            statGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform statRt = statGo.GetComponent<RectTransform>();
            statRt.anchorMin = new Vector2(0f, 0f);
            statRt.anchorMax = new Vector2(1f, 0f);
            statRt.pivot = new Vector2(0.5f, 0f);
            statRt.offsetMin = new Vector2(5f, 75f);
            statRt.offsetMax = new Vector2(-5f, 120f);

            string valUnit = (poke.Type == GemType.Nature || poke.Type == GemType.Healing) ? "Heal" : "Dmg";
            int baseVal = poke.BaseValue + poke.EvolutionDamageBonus;
            int nextVal = baseVal + 5;

            TMPro.TextMeshProUGUI statTxt = statGo.GetComponent<TMPro.TextMeshProUGUI>();
            statTxt.text = $"{valUnit}: {baseVal} <color=#00FF88>➔ {nextVal}</color>\n(Limit: {poke.MaxEnergy})";
            statTxt.fontSize = 13f;
            statTxt.alignment = TMPro.TextAlignmentOptions.Center;
            statTxt.color = GetGemColor(poke.Type) * 1.2f;
            if (messageText != null) statTxt.font = messageText.font;

            // Pokémon Ability Text
            GameObject abilityGo = new GameObject("AbilityText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            abilityGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform abilityRt = abilityGo.GetComponent<RectTransform>();
            abilityRt.anchorMin = new Vector2(0f, 0f);
            abilityRt.anchorMax = new Vector2(1f, 0f);
            abilityRt.pivot = new Vector2(0.5f, 0f);
            abilityRt.offsetMin = new Vector2(5f, 10f);
            abilityRt.offsetMax = new Vector2(-5f, 65f);

            TMPro.TextMeshProUGUI abilityTxt = abilityGo.GetComponent<TMPro.TextMeshProUGUI>();
            AttackRule attackRule = BoardManager.GetInstance().GetAttackRule(poke.Type);
            string abilityName = attackRule != null ? attackRule.AttackName : "Ability";
            string abilityDesc = attackRule != null ? attackRule.EffectDescription : "";
            abilityTxt.text = $"<color=#FFFF00>{abilityName}</color>\n<size=11>{abilityDesc}</size>";
            abilityTxt.fontSize = 13f;
            abilityTxt.alignment = TMPro.TextAlignmentOptions.Center;
            abilityTxt.color = Color.white;
            if (messageText != null) abilityTxt.font = messageText.font;

            // Set up click action
            UnityEngine.UI.Button button = cardGo.GetComponent<UnityEngine.UI.Button>();
            PokemonState capturedPoke = poke; // local scope capture
            button.onClick.AddListener(() =>
            {
                Destroy(_evoSelectionPopupInstance);
                onSelected?.Invoke(capturedPoke);
            });
        }

        // 5. Cancel Button
        GameObject cancelGo = new GameObject("CancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        cancelGo.transform.SetParent(borderGo.transform, false);
        
        RectTransform cancelRt = cancelGo.GetComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0f);
        cancelRt.anchorMax = new Vector2(0.5f, 0f);
        cancelRt.pivot = new Vector2(0.5f, 0f);
        cancelRt.sizeDelta = new Vector2(160f, 40f);
        cancelRt.anchoredPosition = new Vector2(0f, 20f);

        Image cancelImg = cancelGo.GetComponent<Image>();
        cancelImg.color = new Color(0.35f, 0.35f, 0.4f, 1f); // Grey button background

        // Button text
        GameObject cancelTextGo = new GameObject("ButtonText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        cancelTextGo.transform.SetParent(cancelGo.transform, false);
        RectTransform cancelTextRt = cancelTextGo.GetComponent<RectTransform>();
        cancelTextRt.anchorMin = Vector2.zero;
        cancelTextRt.anchorMax = Vector2.one;
        cancelTextRt.offsetMin = Vector2.zero;
        cancelTextRt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI cancelTxt = cancelTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        cancelTxt.text = "Cancel";
        cancelTxt.fontSize = 16f;
        cancelTxt.fontStyle = TMPro.FontStyles.Bold;
        cancelTxt.alignment = TMPro.TextAlignmentOptions.Center;
        cancelTxt.color = Color.white;
        if (messageText != null) cancelTxt.font = messageText.font;

        // Set up click action
        UnityEngine.UI.Button cancelButton = cancelGo.GetComponent<UnityEngine.UI.Button>();
        cancelButton.onClick.AddListener(() =>
        {
            Destroy(_evoSelectionPopupInstance);
            onSelected?.Invoke(null); // Pass null to indicate cancel
        });

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    private void ShowEvolutionSuccessPopup(PokemonState poke, int oldVal, int newVal, Action onClose)
    {
        bool isBot = false;
        var bm = BoardManager.GetInstance();
        if (bm != null && bm.Players != null && bm.Players.Length > 1)
        {
            isBot = bm.Players[1].Pokemons.Contains(poke);
        }

        if (_evoSuccessPopupInstance != null)
        {
            Destroy(_evoSuccessPopupInstance);
        }

        Canvas rootCanvas = boardParent != null ? boardParent.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        // 1. Blocker Overlay
        _evoSuccessPopupInstance = new GameObject("EvoSuccessPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _evoSuccessPopupInstance.transform.SetParent(rootCanvas.transform, false);

        RectTransform overlayRt = _evoSuccessPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = _evoSuccessPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        // 2. Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_evoSuccessPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(400f, 380f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        modalImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

        // Inner Border
        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(modalWindow.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4f, 4f);
        borderRt.offsetMax = new Vector2(-4f, -4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.18f, 0.18f, 0.24f, 1f);

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(borderGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(10f, -60f);
        titleRt.offsetMax = new Vector2(-10f, -15f);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = $"{poke.Name.ToUpper()} EVOLVED!";
        titleTxt.fontSize = 26f;
        titleTxt.fontStyle = TMPro.FontStyles.Bold;
        titleTxt.alignment = TMPro.TextAlignmentOptions.Center;
        titleTxt.color = new Color(0f, 1f, 0.6f, 1f); // Neon green
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Pokémon Avatar
        GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(borderGo.transform, false);
        RectTransform avatarRt = avatarGo.GetComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0.5f, 0.5f);
        avatarRt.anchorMax = new Vector2(0.5f, 0.5f);
        avatarRt.pivot = new Vector2(0.5f, 0.5f);
        avatarRt.sizeDelta = new Vector2(80f, 80f);
        avatarRt.anchoredPosition = new Vector2(0f, 70f);

        Image avatarImg = avatarGo.GetComponent<Image>();
        avatarImg.sprite = poke.Avatar;
        avatarImg.preserveAspect = true;

        // 5. Stat change description
        GameObject statGo = new GameObject("StatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        statGo.transform.SetParent(borderGo.transform, false);
        RectTransform statRt = statGo.GetComponent<RectTransform>();
        statRt.anchorMin = new Vector2(0f, 0f);
        statRt.anchorMax = new Vector2(1f, 0f);
        statRt.pivot = new Vector2(0.5f, 0f);
        statRt.offsetMin = new Vector2(15f, 135f);
        statRt.offsetMax = new Vector2(-15f, 185f);

        string valType = (poke.Type == GemType.Nature || poke.Type == GemType.Healing) ? "Healing" : "Damage";

        TMPro.TextMeshProUGUI statTxt = statGo.GetComponent<TMPro.TextMeshProUGUI>();
        statTxt.text = $"{valType}: {oldVal} <color=#00FF88>➔ {newVal}</color>\n<color=#FFFF00>+5 Evolution Bonus!</color>";
        statTxt.fontSize = 18f;
        statTxt.alignment = TMPro.TextAlignmentOptions.Center;
        statTxt.color = Color.white;
        if (messageText != null) statTxt.font = messageText.font;

        // 5b. Ability Description
        GameObject abilityGo = new GameObject("AbilityText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        abilityGo.transform.SetParent(borderGo.transform, false);
        RectTransform abilityRt = abilityGo.GetComponent<RectTransform>();
        abilityRt.anchorMin = new Vector2(0f, 0f);
        abilityRt.anchorMax = new Vector2(1f, 0f);
        abilityRt.pivot = new Vector2(0.5f, 0f);
        abilityRt.offsetMin = new Vector2(15f, 70f);
        abilityRt.offsetMax = new Vector2(-15f, 125f);

        TMPro.TextMeshProUGUI abilityTxt = abilityGo.GetComponent<TMPro.TextMeshProUGUI>();
        AttackRule attackRule = BoardManager.GetInstance().GetAttackRule(poke.Type);
        string abilityName = attackRule != null ? attackRule.AttackName : "Ability";
        string abilityDesc = attackRule != null ? attackRule.EffectDescription : "";
        abilityTxt.text = $"<color=#FFFF00>{abilityName}</color>\n<size=14>{abilityDesc}</size>";
        abilityTxt.fontSize = 16f;
        abilityTxt.alignment = TMPro.TextAlignmentOptions.Center;
        abilityTxt.color = Color.white;
        if (messageText != null) abilityTxt.font = messageText.font;

        // 6. Dismiss Button
        GameObject btnGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        btnGo.transform.SetParent(borderGo.transform, false);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.sizeDelta = new Vector2(150f, 40f);
        btnRt.anchoredPosition = new Vector2(0f, 20f);

        Image btnImg = btnGo.GetComponent<Image>();
        btnImg.color = new Color(0f, 1f, 0.6f, 1f); // Neon green button

        // Button text
        GameObject btnTextGo = new GameObject("ButtonText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        btnTextGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTextRt = btnTextGo.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI btnTxt = btnTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        btnTxt.text = "Awesome!";
        btnTxt.fontSize = 16f;
        btnTxt.fontStyle = TMPro.FontStyles.Bold;
        btnTxt.alignment = TMPro.TextAlignmentOptions.Center;
        btnTxt.color = new Color(0.05f, 0.05f, 0.08f, 1f); // dark text
        if (messageText != null) btnTxt.font = messageText.font;

        // Set up click action
        UnityEngine.UI.Button button = btnGo.GetComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(() =>
        {
            if (_evoSuccessPopupInstance != null)
            {
                Destroy(_evoSuccessPopupInstance);
                onClose?.Invoke();
            }
        });

        // Small bounce animation to the popup card
        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

        if (isBot)
        {
            StartCoroutine(AutoCloseSuccessPopup(1.5f, onClose));
        }
    }

    private void SetupPokemonClickListeners()
    {
        if (p1Poke1Avatar != null)
        {
            var btn = p1Poke1Avatar.gameObject.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = p1Poke1Avatar.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPokemonAvatarClicked(0, 0));
        }
        if (p1Poke2Avatar != null)
        {
            var btn = p1Poke2Avatar.gameObject.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = p1Poke2Avatar.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPokemonAvatarClicked(0, 1));
        }
    }

    private void OnPokemonAvatarClicked(int playerIdx, int pokemonIdx)
    {
        BoardManager board = BoardManager.GetInstance();
        if (board == null || board.Players == null) return;
        if (board.ActivePlayerIndex != playerIdx) return; // only on own turn
        if (board.IsProcessing || board.IsWaitingForEvolutionSelection) return;
        if (board.Players[playerIdx].MovesRemaining <= 0) return; // needs at least 1 move

        PlayerState player = board.Players[playerIdx];
        PokemonState poke = player.Pokemons[pokemonIdx];

        ShowManualEvolutionPopup(playerIdx, poke);
    }

    private void ShowManualEvolutionPopup(int playerIdx, PokemonState poke)
    {
        if (_manualEvoPopupInstance != null)
        {
            Destroy(_manualEvoPopupInstance);
        }

        Canvas rootCanvas = boardParent != null ? boardParent.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        PlayerState player = BoardManager.GetInstance().Players[playerIdx];
        bool canEvolve = player.EvolutionStones >= PlayerState.EvolutionRequired;

        // 1. Blocker Overlay
        _manualEvoPopupInstance = new GameObject("ManualEvoPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _manualEvoPopupInstance.transform.SetParent(rootCanvas.transform, false);

        RectTransform overlayRt = _manualEvoPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = _manualEvoPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        // 2. Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_manualEvoPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(400f, 250f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        modalImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

        // Inner Border
        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(modalWindow.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4f, 4f);
        borderRt.offsetMax = new Vector2(-4f, -4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.18f, 0.18f, 0.24f, 1f);

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(borderGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(10f, -50f);
        titleRt.offsetMax = new Vector2(-10f, -15f);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = canEvolve ? "EVOLUTION AVAILABLE" : "EVOLUTION LOCKED";
        titleTxt.fontSize = 22f;
        titleTxt.fontStyle = TMPro.FontStyles.Bold;
        titleTxt.alignment = TMPro.TextAlignmentOptions.Center;
        titleTxt.color = canEvolve ? new Color(0f, 1f, 0.6f, 1f) : new Color(0.9f, 0.3f, 0.3f, 1f);
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Description Text
        GameObject descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        descGo.transform.SetParent(borderGo.transform, false);
        RectTransform descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(15f, 80f);
        descRt.offsetMax = new Vector2(-15f, -60f);

        TMPro.TextMeshProUGUI descTxt = descGo.GetComponent<TMPro.TextMeshProUGUI>();
        if (canEvolve)
        {
            descTxt.text = $"Do you want to evolve {poke.Name}?\n<color=#FFFF00>(Costs 1 Move & resets Evolution Stones)</color>";
        }
        else
        {
            descTxt.text = $"{poke.Name} requires {PlayerState.EvolutionRequired} evolution stones to evolve. You currently have {player.EvolutionStones}/{PlayerState.EvolutionRequired}.";
        }
        descTxt.fontSize = 16f;
        descTxt.alignment = TMPro.TextAlignmentOptions.Center;
        descTxt.color = Color.white;
        if (messageText != null) descTxt.font = messageText.font;

        // 5. Buttons Container
        if (canEvolve)
        {
            // Evolve Button
            GameObject evolveGo = new GameObject("EvolveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            evolveGo.transform.SetParent(borderGo.transform, false);
            RectTransform evolveRt = evolveGo.GetComponent<RectTransform>();
            evolveRt.anchorMin = new Vector2(0.5f, 0f);
            evolveRt.anchorMax = new Vector2(0.5f, 0f);
            evolveRt.pivot = new Vector2(0.5f, 0f);
            evolveRt.sizeDelta = new Vector2(130f, 40f);
            evolveRt.anchoredPosition = new Vector2(-75f, 20f);

            Image evolveImg = evolveGo.GetComponent<Image>();
            evolveImg.color = new Color(0f, 1f, 0.6f, 1f); // Green

            GameObject evolveTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            evolveTextGo.transform.SetParent(evolveGo.transform, false);
            RectTransform evolveTextRt = evolveTextGo.GetComponent<RectTransform>();
            evolveTextRt.anchorMin = Vector2.zero;
            evolveTextRt.anchorMax = Vector2.one;
            evolveTextRt.offsetMin = Vector2.zero;
            evolveTextRt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI evolveTxt = evolveTextGo.GetComponent<TMPro.TextMeshProUGUI>();
            evolveTxt.text = "Evolve";
            evolveTxt.fontSize = 16f;
            evolveTxt.fontStyle = TMPro.FontStyles.Bold;
            evolveTxt.alignment = TMPro.TextAlignmentOptions.Center;
            evolveTxt.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            if (messageText != null) evolveTxt.font = messageText.font;

            UnityEngine.UI.Button evolveBtn = evolveGo.GetComponent<UnityEngine.UI.Button>();
            evolveBtn.onClick.AddListener(() =>
            {
                Destroy(_manualEvoPopupInstance);
                BoardManager.GetInstance().TryManualEvolve(playerIdx, poke);
            });

            // Cancel Button
            GameObject cancelGo = new GameObject("CancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            cancelGo.transform.SetParent(borderGo.transform, false);
            RectTransform cancelRt = cancelGo.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.5f, 0f);
            cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.sizeDelta = new Vector2(130f, 40f);
            cancelRt.anchoredPosition = new Vector2(75f, 20f);

            Image cancelImg = cancelGo.GetComponent<Image>();
            cancelImg.color = new Color(0.35f, 0.35f, 0.4f, 1f); // Grey

            GameObject cancelTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            cancelTextGo.transform.SetParent(cancelGo.transform, false);
            RectTransform cancelTextRt = cancelTextGo.GetComponent<RectTransform>();
            cancelTextRt.anchorMin = Vector2.zero;
            cancelTextRt.anchorMax = Vector2.one;
            cancelTextRt.offsetMin = Vector2.zero;
            cancelTextRt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI cancelTxt = cancelTextGo.GetComponent<TMPro.TextMeshProUGUI>();
            cancelTxt.text = "Cancel";
            cancelTxt.fontSize = 16f;
            cancelTxt.fontStyle = TMPro.FontStyles.Bold;
            cancelTxt.alignment = TMPro.TextAlignmentOptions.Center;
            cancelTxt.color = Color.white;
            if (messageText != null) cancelTxt.font = messageText.font;

            UnityEngine.UI.Button cancelBtn = cancelGo.GetComponent<UnityEngine.UI.Button>();
            cancelBtn.onClick.AddListener(() =>
            {
                Destroy(_manualEvoPopupInstance);
            });
        }
        else
        {
            // Close Button
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            closeGo.transform.SetParent(borderGo.transform, false);
            RectTransform closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.sizeDelta = new Vector2(150f, 40f);
            closeRt.anchoredPosition = new Vector2(0f, 20f);

            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.35f, 0.35f, 0.4f, 1f); // Grey

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTextRt = closeTextGo.GetComponent<RectTransform>();
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI closeTxt = closeTextGo.GetComponent<TMPro.TextMeshProUGUI>();
            closeTxt.text = "Close";
            closeTxt.fontSize = 16f;
            closeTxt.fontStyle = TMPro.FontStyles.Bold;
            closeTxt.alignment = TMPro.TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            if (messageText != null) closeTxt.font = messageText.font;

            UnityEngine.UI.Button closeBtn = closeGo.GetComponent<UnityEngine.UI.Button>();
            closeBtn.onClick.AddListener(() =>
            {
                Destroy(_manualEvoPopupInstance);
            });
        }

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    private IEnumerator AutoCloseSuccessPopup(float delay, Action onClose)
    {
        yield return new WaitForSeconds(delay);
        if (_evoSuccessPopupInstance != null)
        {
            Destroy(_evoSuccessPopupInstance);
            onClose?.Invoke();
        }
    }
}
