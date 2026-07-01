using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using AdsManager;

public class BoardInputHandler : MonoBehaviour
{
    [SerializeField] private Transform boardParent;
    [SerializeField] private float cellSize = 100f; // Inspector fallback — overridden at runtime by AspectRatio.MaxBoardCellSize
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Color charryColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color borderColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private float borderWidth = 3f;
    [SerializeField] private Color boardBgColor = new Color(0.05f, 0.05f, 0.05f, 0.7f);
    [SerializeField] private float charryScale = 0.40f;

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
        new Color(0.95f, 0.10f, 0.10f), // Charry — crimson-red Charry stone
    };
    private static readonly Color _pipEmptyColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
    private static readonly Color _activePanel   = new Color(0.15f, 0.15f, 0.18f, 0.95f);
    private static readonly Color _inactivePanel = new Color(0.03f, 0.03f, 0.05f, 0.7f);

    // Drag committed flag to suppress redundant OnGemDrag calls after swap fires
    private bool _dragCommitted;

    // Charry stone sprite — loaded once from Resources/Gems/charry
    private Sprite _evolutionStoneSprite;
    // Charry stone highlight color for board cells
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
    [SerializeField] private Sprite evolutionBadgeSprite;

    [Header("P1 Info")]
    [SerializeField] private TMPro.TextMeshProUGUI p1NameText;
    [SerializeField] private TMPro.TextMeshProUGUI p1MovesText;
    [SerializeField] private TMPro.TextMeshProUGUI p1HpText;
    [SerializeField] private Image p1HpBar;
    [SerializeField] private Image p1HpBarTrailing;

    [Header("P1 Creature 1")]
    [SerializeField] private Image p1Poke1Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1Name;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1EnergyText;
    [SerializeField] private Image p1Poke1EnergyBar;
    [SerializeField] private Image p1Poke1Stone;
    [SerializeField] private Transform p1Poke1EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p1Poke1AttackLabel;

    [Header("P1 Creature 2")]
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

    [Header("P2 Creature 1")]
    [SerializeField] private Image p2Poke1Avatar;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1Name;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1EnergyText;
    [SerializeField] private Image p2Poke1EnergyBar;
    [SerializeField] private Image p2Poke1Stone;
    [SerializeField] private Transform p2Poke1EnergyBgTransform;
    [SerializeField] private TMPro.TextMeshProUGUI p2Poke1AttackLabel;

    [Header("P2 Creature 2")]
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
    // Layout: index = playerIdx * 2 + creatureIdx  (0=P1/Poke1, 1=P1/Poke2, 2=P2/Poke1, 3=P2/Poke2)
    private Image[][] creaturePips          = new Image[4][];
    private TMPro.TextMeshProUGUI[] creatureAttackLabels = new TMPro.TextMeshProUGUI[4];
    private Transform[] creatureEnergyBgTransforms       = new Transform[4];
    private int _creatureRowIndex; // incremented inside CreateCreatureRow

    private Vector3 _originalMessagePos;
    private float _messageTextBaseFontSize = 30f;
    private const float MessageTextMinFontSize = 16f;
    private const int MessageTextMaxLines = 2;
    private float _displayedP1HP = -1f;
    private float _displayedP2HP = -1f;
    private int _displayedP1EvolutionStones = -1;
    private int _displayedP2EvolutionStones = -1;
    private bool _creatureEvoBadgesReady;
    private readonly Image[] _creatureEvoBadges = new Image[4];
    private static Sprite _cachedEvolutionBadgeSprite;
    private const float CreatureNameFontSize = 24f;

    private const float CharryUIGroupPosX = 15f;
    private const float CharryUIGroupPosY = -60f;

    private GameObject _gameOverPopupInstance;
    private GameObject _evoSelectionPopupInstance;
    private GameObject _evoSuccessPopupInstance;

    private const float BattleModalW           = 900f;
    private const float BattleModalH           = 1500f;
    private const float BattleModalTitleFont   = 46f;
    private const float BattleModalBodyFont    = 28f;
    private const float BattleModalStatFont    = 24f;
    private const float BattleModalAbilityFont = 22f;
    private const float BattleModalAvatarSize  = 300f;
    private const float BattleModalBtnW        = 200f;
    private const float BattleModalBtnH        = 70f;
    private const float BattleModalBtnBottomY  = 100f;
    private const float BattleModalOkBtnW      = 300f;
    private const float BattleModalOkBtnH      = 180f;
    private const float BattleModalTitleY      = -250f;
    private const float ManualEvoAvatarSize    = 300f;
    private const float GameOverPopupFont      = 45f;

    private static readonly Color BattlePopupTitleColor = new Color(0.745283f, 0.56290144f, 0.28475437f, 1f);
    private static readonly Color BattlePopupBodyColor  = new Color(1f, 0.7273872f, 0.5518868f, 1f);
    private const string BattleHighlightGold = "#FFD700";
    private const string BattleHighlightGreen = "#7CFC9A";

    private const string ThemeVictoryGreen = "#00FF88";
    private const string ThemeDefeatRed    = "#FF4444";
    private const string ThemeDrawGold     = "#FFCC00";
    private const string ThemeCoinGold     = "#FFE600";
    private const string ThemeXpCyan       = "#00EAFF";

    private void Start()
    {
        // ── Safe-area canvas adjustment ────────────────────────────────────────
        // Shrinks the root Canvas RectTransform to fit inside the device safe area
        // so UI is never hidden behind notches, camera punch-outs, or home bars.
        ApplySafeArea();

        if (messageText != null)
        {
            _originalMessagePos = messageText.transform.localPosition;
            _messageTextBaseFontSize = messageText.fontSize;
            messageText.alpha = 0f;
        }

        evolutionBadgeSprite = GetEvolutionBadgeSprite();
        EnsureCreatureEvoBadges();
        ConfigureCreatureNameLabels();

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
            gemSprites[6] = Resources.Load<Sprite>("Gems/charry");
        if (gemSprites[6] != null)
            anyLoaded = true;

        if (!anyLoaded)
        {
            UnityEngine.Debug.LogWarning("[BoardInputHandler] No gem sprites found. " +
                "Place gem sprites in Assets/Resources/Gems/ named element_block_1..element_block_6 " +
                "and charry, or assign the `gemSprites` array in the inspector.");
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
        
        StartCoroutine(ShowAdBannerRoutine());
    }

    private IEnumerator ShowAdBannerRoutine()
    {
        yield return new WaitForSeconds(0.5f); // Wait for scene transition to settle

        if (AdMobManager.GetInstance() != null)
        {
            yield return new WaitUntil(() => AdMobManager.GetInstance().IsSdkInitialized);
            
            // Temporarily disable destroying the old banner so we can reuse it instantly
            bool originalSetting = AdMobManager.GetInstance().LoadNewBannerOnEachRequest;
            AdMobManager.GetInstance().LoadNewBannerOnEachRequest = false;
            
            AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom, AdStatusDelegate: OnAdStatus);
            
            // Restore the original setting
            AdMobManager.GetInstance().LoadNewBannerOnEachRequest = originalSetting;
        }
    }

    private void OnAdStatus(AdStatusCode code)
    {
        if (code == AdStatusCode.ADLoadSuccess && AdMobManager.GetInstance() != null)
        {
            // Call ShowBanner via a coroutine to ensure it doesn't conflict with HideBanner in the same frame
            StartCoroutine(ShowBannerNextFrame());
        }
    }

    private IEnumerator ShowBannerNextFrame()
    {
        yield return null; // Wait one frame
        
        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().ShowBanner();


        }
    }

    private void OnDestroy()
    {
        BoardManager.OnBoardInitialized -= OnBoardInit;
        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().HideBanner();
            AdMobManager.GetInstance().DestroyBanner();
        }
    }

    /// <summary>
    /// Adjusts the root Canvas RectTransform to sit inside the device safe area.
    /// This prevents UI elements from being obscured by notches or home indicators.
    /// </summary>
    private void ApplySafeArea()
    {
        // Handled globally by UIAdapterManager to avoid canvas conflict and ensure perfect layouts.
    }

    /// <summary>Resolves the cell size to use for the current device screen.</summary>
    private float ResolveCellSize()
    {
        return 90f;
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
                    // Only restore scale for visible cells — Empty (hidden) cells
                    // must stay at scale 0 so they don’t flash back into view.
                    bool isEmpty = board?.Grid != null
                                 && board.Grid.Grid[r, c] == GemType.Empty;
                    if (!isEmpty)
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
        creaturePips               = new Image[4][];
        creatureAttackLabels       = new TMPro.TextMeshProUGUI[4];
        creatureEnergyBgTransforms = new Transform[4];

        _displayedP1HP = -1f;
        _displayedP2HP = -1f;
        _displayedP1EvolutionStones = -1;
        _displayedP2EvolutionStones = -1;
        // ────────────────────────────────────────────────────────────────────────

        CreateGrid();
        CreatePlayerUI();
        RefreshBoard();
        InitPips();   // build segmented pip bars now that Creature types are known
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
        boardRect.sizeDelta = new Vector2(1065f, 835f);

        boardRect.anchoredPosition = new Vector2(57f, -340f);

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
            // Screen Y grows upward; grid row grows downward — invert drag Y.
            to.y += delta.y > 0 ? -1 : 1;

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

                bool isEmpty  = idx == (int)GemType.Empty;
                bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
                bool isFalling = fallToSet.Contains(new Vector2Int(c, r));
                bool isNew     = !isFalling && newSet.Contains(new Vector2Int(c, r));

                if (isEmpty)
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
                    img.transform.localScale = (gem == GemType.Charry) ? new Vector3(charryScale, charryScale, 1f) : Vector3.one;

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
                    img.transform.localScale = (gem == GemType.Charry) ? new Vector3(charryScale, charryScale, 1f) : Vector3.one;
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
                    img.transform.localScale = (gem == GemType.Charry) ? new Vector3(charryScale, charryScale, 1f) : Vector3.one;
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
            if (gem2 == GemType.Empty) continue;

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
            if (gem2 == GemType.Empty) continue;

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
                bool isEmpty = idx == (int)GemType.Empty;

                var canvasGroup = img.GetComponent<CanvasGroup>();

                rt.anchoredPosition = new Vector2(
                    c * (_resolvedCellSize + spacing) - totalW / 2f + _resolvedCellSize / 2f,
                    -(r * (_resolvedCellSize + spacing) - totalH / 2f + _resolvedCellSize / 2f)
                );

                if (isEmpty)
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
                    img.transform.localScale = (gem == GemType.Charry) ? new Vector3(charryScale, charryScale, 1f) : Vector3.one;
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
        creatureAttackLabels[0] = p1Poke1AttackLabel;
        creatureAttackLabels[1] = p1Poke2AttackLabel;
        creatureAttackLabels[2] = p2Poke1AttackLabel;
        creatureAttackLabels[3] = p2Poke2AttackLabel;

        creatureEnergyBgTransforms[0] = p1Poke1EnergyBgTransform;
        creatureEnergyBgTransforms[1] = p1Poke2EnergyBgTransform;
        creatureEnergyBgTransforms[2] = p2Poke1EnergyBgTransform;
        creatureEnergyBgTransforms[3] = p2Poke2EnergyBgTransform;

        EnforceFilledImage(p1HpBar);
        EnforceFilledImage(p1HpBarTrailing);
        EnforceFilledImage(p2HpBar);
        EnforceFilledImage(p2HpBarTrailing);
        EnforceFilledImage(p1Poke1EnergyBar);
        EnforceFilledImage(p1Poke2EnergyBar);
        EnforceFilledImage(p2Poke1EnergyBar);
        EnforceFilledImage(p2Poke2EnergyBar);

        // Configure click listeners for opponent creatures programmatically
        if (p2Poke1Avatar != null)
        {
            var btn = p2Poke1Avatar.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = p2Poke1Avatar.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnP2Poke1AvatarClicked);
        }
        if (p2Poke2Avatar != null)
        {
            var btn = p2Poke2Avatar.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = p2Poke2Avatar.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnP2Poke2AvatarClicked);
        }

        ConfigureCreatureNameLabels();

        EnsureEvolutionUI(0);
        EnsureEvolutionUI(1);
        UpdatePlayerUI();
    }

    private void ConfigureCreatureNameLabels()
    {
        TMPro.TextMeshProUGUI[] labels = { p1Poke1Name, p1Poke2Name, p2Poke1Name, p2Poke2Name };
        foreach (var label in labels)
        {
            if (label == null) continue;

            label.enableAutoSizing = false;
            label.fontSizeMin = CreatureNameFontSize;
            label.fontSizeMax = CreatureNameFontSize;
            label.fontSize = CreatureNameFontSize;

            RectTransform rt = label.rectTransform;
            if (rt.sizeDelta.y < CreatureNameFontSize + 6f)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, CreatureNameFontSize + 6f);
        }
    }

    private void EnsureEvolutionUI(int playerIndex)
    {
        Image panelBg = playerIndex == 0 ? p1PanelBg : p2PanelBg;
        Transform group = panelBg != null ? panelBg.transform.Find("CharryUI_Group") : null;
        if (group != null)
            group.gameObject.SetActive(true);

        Sprite charrySprite = (gemSprites != null && gemSprites.Length > 6) ? gemSprites[6] : fallbackGemSprite;
        Image icon = playerIndex == 0 ? p1EvoStoneIcon : p2EvoStoneIcon;
        TMPro.TextMeshProUGUI text = playerIndex == 0 ? p1EvoStoneText : p2EvoStoneText;

        if (IsAlive(icon))
        {
            icon.sprite = charrySprite;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(true);
        }
        if (IsAlive(text))
            text.gameObject.SetActive(true);
    }

    private void UpdateEvolutionStoneDisplay(BoardManager board)
    {
        if (board?.Players == null) return;

        int p1Stones = board.Players[0].EvolutionStones;
        if (IsAlive(p1EvoStoneText) && p1Stones != _displayedP1EvolutionStones)
        {
            _displayedP1EvolutionStones = p1Stones;
            p1EvoStoneText.text = p1Stones + "/" + PlayerState.EvolutionRequired;
        }
        if (IsAlive(p1EvoGlow))
        {
            bool anyEvolved = board.Players[0].Creatures.Exists(p => p.IsEvolved);
            p1EvoGlow.gameObject.SetActive(anyEvolved);
        }

        int p2Stones = board.Players[1].EvolutionStones;
        if (IsAlive(p2EvoStoneText) && p2Stones != _displayedP2EvolutionStones)
        {
            _displayedP2EvolutionStones = p2Stones;
            p2EvoStoneText.text = p2Stones + "/" + PlayerState.EvolutionRequired;
        }
        if (IsAlive(p2EvoGlow))
        {
            bool anyEvolved = board.Players[1].Creatures.Exists(p => p.IsEvolved);
            p2EvoGlow.gameObject.SetActive(anyEvolved);
        }
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

        AdjustUIPanelScale();

        // P1 Main Info
        p1NameText.text = board.Players[0].Name;
        if (p1MovesText != null)
        {
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

        // P1 Creature 1
        var p1Poke1 = board.Players[0].Creatures[0];
        p1Poke1Avatar.sprite = p1Poke1.Avatar;
        if (p1Poke1Avatar != null) p1Poke1Avatar.preserveAspect = true;
        UpdateCreatureNameRow(0, p1Poke1Name, p1Poke1);
        p1Poke1EnergyText.text = "Gems: " + p1Poke1.CurrentEnergy + "/" + p1Poke1.MaxEnergy;
        if (p1Poke1EnergyBar != null && p1Poke1EnergyBar) p1Poke1EnergyBar.fillAmount = (float)p1Poke1.CurrentEnergy / p1Poke1.MaxEnergy;
        if (IsAlive(p1Poke1Stone))
        {
            int idx = (int)p1Poke1.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p1Poke1Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p1Poke1Stone.color = hasSprite ? Color.white : GetGemColor(p1Poke1.Type);
        }

        // P1 Creature 2
        var p1Poke2 = board.Players[0].Creatures[1];
        p1Poke2Avatar.sprite = p1Poke2.Avatar;
        if (p1Poke2Avatar != null) p1Poke2Avatar.preserveAspect = true;
        UpdateCreatureNameRow(1, p1Poke2Name, p1Poke2);
        p1Poke2EnergyText.text = "Gems: " + p1Poke2.CurrentEnergy + "/" + p1Poke2.MaxEnergy;
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

        // P2 Creature 1
        var p2Poke1 = board.Players[1].Creatures[0];
        p2Poke1Avatar.sprite = p2Poke1.Avatar;
        if (p2Poke1Avatar != null) p2Poke1Avatar.preserveAspect = true;
        UpdateCreatureNameRow(2, p2Poke1Name, p2Poke1);
        p2Poke1EnergyText.text = "Gems: " + p2Poke1.CurrentEnergy + "/" + p2Poke1.MaxEnergy;
        if (p2Poke1EnergyBar != null && p2Poke1EnergyBar) p2Poke1EnergyBar.fillAmount = (float)p2Poke1.CurrentEnergy / p2Poke1.MaxEnergy;
        if (IsAlive(p2Poke1Stone))
        {
            int idx = (int)p2Poke1.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p2Poke1Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p2Poke1Stone.color = hasSprite ? Color.white : GetGemColor(p2Poke1.Type);
        }

        // P2 Creature 2
        var p2Poke2 = board.Players[1].Creatures[1];
        p2Poke2Avatar.sprite = p2Poke2.Avatar;
        if (p2Poke2Avatar != null) p2Poke2Avatar.preserveAspect = true;
        UpdateCreatureNameRow(3, p2Poke2Name, p2Poke2);
        p2Poke2EnergyText.text = "Gems: " + p2Poke2.CurrentEnergy + "/" + p2Poke2.MaxEnergy;
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

        UpdateEvolutionStoneDisplay(board);

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
    // Pip charge bar initialisation — called once after board + creature are set
    // -------------------------------------------------------------------------
    private void InitPips()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board?.Players == null) return;

        for (int p = 0; p < 2; p++)
        {
            for (int k = 0; k < board.Players[p].Creatures.Count && k < 2; k++)
            {
                int idx = p * 2 + k;
                Transform bgTf = creatureEnergyBgTransforms[idx];
                if (bgTf == null) continue;

                // Destroy only previously created pips (sparing the hidden EnergyFill)
                for (int ci = bgTf.childCount - 1; ci >= 0; ci--)
                {
                    GameObject child = bgTf.GetChild(ci).gameObject;
                    if (child.name.StartsWith("Pip_"))
                        Destroy(child);
                }

                CreatureState poke = board.Players[p].Creatures[k];
                AttackRule rule   = board.GetAttackRule(poke.Type);
                int stonesRequired = poke.MaxEnergy;
                int n = stonesRequired; // Creature-specific energy limit (max 9)

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
                creaturePips[idx] = pips;

                // Attack label: e.g. "Collect 6 Fire → Ember (10 dmg)"
                if (creatureAttackLabels[idx] != null)
                {
                    string valUnit = (poke.Type == GemType.Nature || poke.Type == GemType.Healing) ? "heal" : "dmg";
                    int actualVal = poke.BaseValue + poke.EvolutionDamageBonus;
                    string dmgPart = $" ({actualVal} {valUnit})";
                    creatureAttackLabels[idx].text =
                        $"Collect {n} {poke.Type} → {rule.AttackName}{dmgPart}";
                    creatureAttackLabels[idx].color = GetGemColor(poke.Type) * 0.9f;
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
            for (int k = 0; k < board.Players[p].Creatures.Count && k < 2; k++)
            {
                int idx      = p * 2 + k;
                Image[] pips = creaturePips[idx];
                if (pips == null) continue;

                CreatureState poke = board.Players[p].Creatures[k];
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

    private void FitMessageTextToTwoLines(string message)
    {
        messageText.enableAutoSizing = false;
        messageText.text = message;
        messageText.fontSize = _messageTextBaseFontSize;
        messageText.ForceMeshUpdate();

        while (messageText.textInfo.lineCount > MessageTextMaxLines
               && messageText.fontSize > MessageTextMinFontSize)
        {
            messageText.fontSize -= 1f;
            messageText.ForceMeshUpdate();
        }
    }

    private void ShowMessage(string message)
    {
        // messageText is a child of MessageBanner; bail if destroyed.
        if (messageText == null || !messageText) return;
        
        // Reset base state
        messageText.transform.DOKill(complete: true);
        
        // Keep messageText perfectly centered inside the banner
        messageText.transform.localPosition = Vector3.zero;
        
        FitMessageTextToTwoLines(message);
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
        // Refresh pip bars whenever any Creature collects stones mid-turn
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
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        // 2. Create Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_gameOverPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(850f, 850f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        // 3. Create Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(modalWindow.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f, 80f);
        titleRt.anchoredPosition = new Vector2(0f, BattleModalTitleY);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = "GAME OVER";
        StyleBattlePopupTitle(titleTxt);
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Create Message Text (Winner / Loser details)
        GameObject msgGo = new GameObject("MessageText", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
        msgGo.transform.SetParent(modalWindow.transform, false);
        RectTransform msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.5f, 0.5f);
        msgRt.anchorMax = new Vector2(0.5f, 0.5f);
        msgRt.pivot = new Vector2(0.5f, 0.5f);
        msgRt.sizeDelta = new Vector2(700f, 340f);
        msgRt.anchoredPosition = new Vector2(0f, -20f);

        var vlg = msgGo.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.spacing = 20f;

        var profile = PlayerProfileManager.GetInstance();
        int earnedXp = profile != null ? profile.LastEarnedXP : 0;
        int earnedCoins = profile != null ? profile.LastEarnedCoins : 0;

        // Title Row
        GameObject titleRow = new GameObject("ResultTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleRow.transform.SetParent(msgGo.transform, false);
        TMPro.TextMeshProUGUI titleRowTxt = titleRow.GetComponent<TMPro.TextMeshProUGUI>();
        titleRowTxt.fontSize = 40f;
        titleRowTxt.alignment = TMPro.TextAlignmentOptions.Center;
        if (messageText != null) titleRowTxt.font = messageText.font;
        
        if (losingPlayerIdx == 1) titleRowTxt.text = $"<color={ThemeVictoryGreen}>Victory!</color>";
        else if (losingPlayerIdx == 0) titleRowTxt.text = $"<color={ThemeDefeatRed}>Defeat!</color>";
        else titleRowTxt.text = $"<color={ThemeDrawGold}>Draw!</color>";

        // Coins Row (Amount + Image)
        GameObject coinsRow = new GameObject("CoinsRow", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        coinsRow.transform.SetParent(msgGo.transform, false);
        var hlg = coinsRow.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.spacing = 15f;

        GameObject coinValGo = new GameObject("CoinVal", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        coinValGo.transform.SetParent(coinsRow.transform, false);
        TMPro.TextMeshProUGUI coinValTxt = coinValGo.GetComponent<TMPro.TextMeshProUGUI>();
        coinValTxt.fontSize = 40f;
        if (messageText != null) coinValTxt.font = messageText.font;
        coinValTxt.text = $"<color={ThemeCoinGold}>+{(losingPlayerIdx == 0 ? 0 : earnedCoins)}</color>";
        coinValTxt.alignment = TMPro.TextAlignmentOptions.Center;
        coinValTxt.enableWordWrapping = false;

        GameObject coinIconGo = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coinIconGo.transform.SetParent(coinsRow.transform, false);
        var layoutElem = coinIconGo.AddComponent<UnityEngine.UI.LayoutElement>();
        layoutElem.minWidth = 50f;
        layoutElem.minHeight = 50f;
        layoutElem.preferredWidth = 50f;
        layoutElem.preferredHeight = 50f;
        var coinIcon = coinIconGo.GetComponent<Image>();
        Sprite[] uiSprites = Resources.LoadAll<Sprite>("UI/UI-pack_Sprite_1");
        Sprite cSprite = System.Array.Find(uiSprites, s => s.name.EndsWith("11"));
        if (cSprite != null) coinIcon.sprite = cSprite;

        // XP Row
        GameObject xpRow = new GameObject("XPRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        xpRow.transform.SetParent(msgGo.transform, false);
        TMPro.TextMeshProUGUI xpRowTxt = xpRow.GetComponent<TMPro.TextMeshProUGUI>();
        xpRowTxt.fontSize = 40f;
        xpRowTxt.alignment = TMPro.TextAlignmentOptions.Center;
        if (messageText != null) xpRowTxt.font = messageText.font;
        xpRowTxt.text = $"<color={ThemeXpCyan}>+{earnedXp} XP</color>";
        xpRowTxt.enableWordWrapping = false;

        Sprite[] newButtonSprites = Resources.LoadAll<Sprite>("buttons/new-buttons");
        Sprite yesBtnSprite = System.Array.Find(popupSprites, s => s.name == "popup_0");
        if (yesBtnSprite == null)
            yesBtnSprite = System.Array.Find(newButtonSprites, s => s.name == "new-buttons_8");
        Sprite noBtnSprite = System.Array.Find(popupSprites, s => s.name == "popup_1");
        if (noBtnSprite == null)
            noBtnSprite = System.Array.Find(newButtonSprites, s => s.name == "new-buttons_9");

        // 5. Yes — start a new battle with the same team
        GameObject yesBtnGo = new GameObject("YesButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        yesBtnGo.transform.SetParent(modalWindow.transform, false);
        RectTransform yesBtnRt = yesBtnGo.GetComponent<RectTransform>();
        yesBtnRt.anchorMin = new Vector2(0.5f, 0f);
        yesBtnRt.anchorMax = new Vector2(0.5f, 0f);
        yesBtnRt.pivot = new Vector2(0.5f, 0f);
        yesBtnRt.sizeDelta = new Vector2(BattleModalBtnW, BattleModalBtnH);
        yesBtnRt.anchoredPosition = new Vector2(-110f, BattleModalBtnBottomY);

        Image yesBtnImg = yesBtnGo.GetComponent<Image>();
        if (yesBtnSprite != null)
        {
            yesBtnImg.sprite = yesBtnSprite;
            yesBtnImg.type = Image.Type.Simple;
        }
        yesBtnImg.color = Color.white;

        yesBtnGo.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnGameOverYesClicked);

        // 6. No — return to home scene
        GameObject noBtnGo = new GameObject("NoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        noBtnGo.transform.SetParent(modalWindow.transform, false);
        RectTransform noBtnRt = noBtnGo.GetComponent<RectTransform>();
        noBtnRt.anchorMin = new Vector2(0.5f, 0f);
        noBtnRt.anchorMax = new Vector2(0.5f, 0f);
        noBtnRt.pivot = new Vector2(0.5f, 0f);
        noBtnRt.sizeDelta = new Vector2(BattleModalBtnW, BattleModalBtnH);
        noBtnRt.anchoredPosition = new Vector2(110f, BattleModalBtnBottomY);

        Image noBtnImg = noBtnGo.GetComponent<Image>();
        if (noBtnSprite != null)
        {
            noBtnImg.sprite = noBtnSprite;
            noBtnImg.type = Image.Type.Simple;
        }
        noBtnImg.color = Color.white;

        noBtnGo.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnGameOverNoClicked);

        // Small bounce animation to the popup card
        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    private void DismissGameOverPopup()
    {
        if (_gameOverPopupInstance != null)
        {
            Destroy(_gameOverPopupInstance);
            _gameOverPopupInstance = null;
        }
    }

    private void OnGameOverYesClicked()
    {
        DismissGameOverPopup();

        var profile = PlayerProfileManager.GetInstance();
        if (profile != null)
        {
            if (!profile.CanAffordBattle)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_MENU);
                return;
            }

            profile.SpendCoinsForBattle(profile.SelectedBet);
            profile.SetActiveBet(profile.SelectedBet);
        }

        BoardManager board = BoardManager.GetInstance();
        if (board != null)
            board.RestartBattle();
    }

    private void OnGameOverNoClicked()
    {
        DismissGameOverPopup();
        UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_MENU);
    }

    private void ShowEvolutionSelectionPopup(int playerIdx, Action<CreatureState> onSelected)
    {
        if (playerIdx == 1)
        {
            PlayerState botPlayer = BoardManager.GetInstance().Players[1];
            CreatureState chosen = null;
            foreach (var p in botPlayer.Creatures)
            {
                if (!p.IsEvolved)
                {
                    chosen = p;
                    break;
                }
            }
            if (chosen == null && botPlayer.Creatures.Count > 0)
            {
                chosen = botPlayer.Creatures[UnityEngine.Random.Range(0, botPlayer.Creatures.Count)];
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
        modalRt.sizeDelta = new Vector2(BattleModalW, BattleModalH);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(modalWindow.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f, 80f);
        titleRt.anchoredPosition = new Vector2(0f, BattleModalTitleY);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = "CHOOSE CREATURE TO EVOLVE";
        StyleBattlePopupTitle(titleTxt);
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Create columns for the two creatures
        const float cardWidth = 320f;
        const float cardHeight = 850f;
        const float cardSpacing = 36f;
        const float avatarSize = BattleModalAvatarSize;

        for (int i = 0; i < player.Creatures.Count && i < 2; i++)
        {
            CreatureState poke = player.Creatures[i];
            
            // Create Column Panel (Button)
            GameObject cardGo = new GameObject($"PokeCard_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            cardGo.transform.SetParent(modalWindow.transform, false);
            
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
            
            float xOffset = (i == 0) ? -(cardWidth / 2f + cardSpacing / 2f) : (cardWidth / 2f + cardSpacing / 2f);
            cardRt.anchoredPosition = new Vector2(xOffset, -50f);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(1f, 1f, 1f, 0.12f);

            // Outer outline for the creature card
            GameObject cardOutlineGo = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardOutlineGo.transform.SetParent(cardGo.transform, false);
            RectTransform outlineRt = cardOutlineGo.GetComponent<RectTransform>();
            outlineRt.anchorMin = Vector2.zero;
            outlineRt.anchorMax = Vector2.one;
            outlineRt.offsetMin = new Vector2(2f, 2f);
            outlineRt.offsetMax = new Vector2(-2f, -2f);
            Image outlineImg = cardOutlineGo.GetComponent<Image>();
            outlineImg.color = new Color(0.45f, 0.35f, 0.22f, 0.35f);
            outlineImg.raycastTarget = false;

            // Creature Avatar/Sprite
            GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            avatarGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform avatarRt = avatarGo.GetComponent<RectTransform>();
            avatarRt.anchorMin = new Vector2(0.5f, 1f);
            avatarRt.anchorMax = new Vector2(0.5f, 1f);
            avatarRt.pivot = new Vector2(0.5f, 1f);
            avatarRt.sizeDelta = new Vector2(avatarSize, avatarSize);
            avatarRt.anchoredPosition = new Vector2(0f, -28f);

            Image avatarImg = avatarGo.GetComponent<Image>();
            avatarImg.sprite = poke.Avatar;
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;

            // Creature Name
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.offsetMin = new Vector2(8f, 450f);
            nameRt.offsetMax = new Vector2(-8f, 570f);

            TMPro.TextMeshProUGUI nameTxt = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
            nameTxt.text = poke.Name.ToUpper();
            nameTxt.fontSize = 32f;
            nameTxt.fontStyle = TMPro.FontStyles.Bold;
            nameTxt.alignment = TMPro.TextAlignmentOptions.Center;
            nameTxt.color = BattlePopupTitleColor;
            nameTxt.raycastTarget = false;
            if (messageText != null) nameTxt.font = messageText.font;

            // Creature Stats Description
            GameObject statGo = new GameObject("StatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            statGo.transform.SetParent(cardOutlineGo.transform, false);
            RectTransform statRt = statGo.GetComponent<RectTransform>();
            statRt.anchorMin = new Vector2(0f, 0f);
            statRt.anchorMax = new Vector2(1f, 0f);
            statRt.pivot = new Vector2(0.5f, 0f);
            statRt.offsetMin = new Vector2(8f, 20f);
            statRt.offsetMax = new Vector2(-8f, 450f);

            TMPro.TextMeshProUGUI statTxt = statGo.GetComponent<TMPro.TextMeshProUGUI>();
            statTxt.text = BuildCreatureDetailBody(poke);
            StyleBattlePopupBody(statTxt, 20f);
            statTxt.paragraphSpacing = 10f;
            statTxt.verticalAlignment = TMPro.VerticalAlignmentOptions.Top;
            statTxt.raycastTarget = false;
            if (messageText != null) statTxt.font = messageText.font;

            // Set up click action
            UnityEngine.UI.Button button = cardGo.GetComponent<UnityEngine.UI.Button>();
            CreatureState capturedPoke = poke; // local scope capture
            button.onClick.AddListener(() =>
            {
                Destroy(_evoSelectionPopupInstance);
                onSelected?.Invoke(capturedPoke);
            });
        }

        // 5. Close Button
        var closeBtn = CreateBattleOkButton(modalWindow.transform, new Vector2(0f, BattleModalBtnBottomY), () =>
        {
            Destroy(_evoSelectionPopupInstance);
            onSelected?.Invoke(null);
        });

        RectTransform closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(300f, 200f);
        closeRt.anchoredPosition = new Vector2(0f, 150f);

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    private void ShowEvolutionSuccessPopup(CreatureState poke, int oldVal, int newVal, Action onClose)
    {
        bool isBot = false;
        var bm = BoardManager.GetInstance();
        if (bm != null && bm.Players != null && bm.Players.Length > 1)
        {
            isBot = bm.Players[1].Creatures.Contains(poke);
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
        modalRt.sizeDelta = new Vector2(900f, 1600f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(modalWindow.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f * 1.25f, 80f * 1.25f);
        titleRt.anchoredPosition = new Vector2(0f, BattleModalTitleY);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = $"{poke.Name.ToUpper()} EVOLVED!";
        StyleBattlePopupTitle(titleTxt);
        titleTxt.fontSize = BattleModalTitleFont;
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Creature Avatar
        GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(modalWindow.transform, false);
        RectTransform avatarRt = avatarGo.GetComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0.5f, 0.5f);
        avatarRt.anchorMax = new Vector2(0.5f, 0.5f);
        avatarRt.pivot = new Vector2(0.5f, 0.5f);
        avatarRt.sizeDelta = new Vector2(BattleModalAvatarSize, BattleModalAvatarSize);
        avatarRt.anchoredPosition = new Vector2(0f, 300f);

        Image avatarImg = avatarGo.GetComponent<Image>();
        avatarImg.sprite = poke.Avatar;
        avatarImg.preserveAspect = true;

        // 5. Stat change + creature details
        GameObject statGo = new GameObject("StatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        statGo.transform.SetParent(modalWindow.transform, false);
        RectTransform statRt = statGo.GetComponent<RectTransform>();
        statRt.anchorMin = new Vector2(0.5f, 0.5f);
        statRt.anchorMax = new Vector2(0.5f, 0.5f);
        statRt.pivot = new Vector2(0.5f, 0.5f);
        statRt.sizeDelta = new Vector2(650f * 1.25f, 360f * 1.25f);
        statRt.anchoredPosition = new Vector2(0f, -175f);

        string valType = (poke.Type == GemType.Nature || poke.Type == GemType.Healing) ? "healing" : "damage";

        TMPro.TextMeshProUGUI statTxt = statGo.GetComponent<TMPro.TextMeshProUGUI>();
        statTxt.text =
            $"<b>{poke.Name}</b> has evolved and now brings <color={BattleHighlightGreen}><b>{newVal}</b></color> {valType} power, rising from <color={BattleHighlightGold}><b>{oldVal}</b></color> with a <color={BattleHighlightGold}><b>+5</b></color> evolution bonus.\n\n" +
            BuildCreatureDetailBody(poke);
        StyleBattlePopupBody(statTxt, 33f);
        statTxt.paragraphSpacing = 10f;
        if (messageText != null) statTxt.font = messageText.font;

        // 6. Dismiss Button
        var closeBtn = CreateBattleOkButton(modalWindow.transform, new Vector2(130f, 150f), () =>
        {
            if (_evoSuccessPopupInstance != null)
            {
                Destroy(_evoSuccessPopupInstance);
                onClose?.Invoke();
            }
        });
        RectTransform closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(300f, 200f);
        closeRt.anchoredPosition = new Vector2(0f, 150f);
        // Small bounce animation to the popup card
        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

        if (isBot)
        {
            StartCoroutine(AutoCloseSuccessPopup(1.5f, onClose));
        }
    }

    public void OnP1Poke1AvatarClicked()
    {
        OnCreatureAvatarClicked(0, 0);
    }

    public void OnP1Poke2AvatarClicked()
    {
        OnCreatureAvatarClicked(0, 1);
    }

    public void OnP2Poke1AvatarClicked()
    {
        OnCreatureAvatarClicked(1, 0);
    }

    public void OnP2Poke2AvatarClicked()
    {
        OnCreatureAvatarClicked(1, 1);
    }

    private void OnCreatureAvatarClicked(int playerIdx, int creatureIdx)
    {
        BoardManager board = BoardManager.GetInstance();
        if (board == null || board.Players == null) return;
        if (board.IsProcessing || board.IsWaitingForEvolutionSelection) return;

        PlayerState player = board.Players[playerIdx];
        if (player.Creatures == null || creatureIdx >= player.Creatures.Count) return;
        CreatureState poke = player.Creatures[creatureIdx];

        // Only allow actual evolution if it's the player's own turn and they have moves remaining
        bool evolutionAllowed = (playerIdx == 0) && (board.ActivePlayerIndex == 0) && (board.Players[0].MovesRemaining > 0);

        ShowManualEvolutionPopup(playerIdx, poke, evolutionAllowed);
    }

    private void ShowManualEvolutionPopup(int playerIdx, CreatureState poke, bool evolutionAllowed = true)
    {
        if (_manualEvoPopupInstance != null)
        {
            Destroy(_manualEvoPopupInstance);
        }

        Canvas rootCanvas = boardParent != null ? boardParent.GetComponentInParent<Canvas>() : null;
        if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        PlayerState player = BoardManager.GetInstance().Players[playerIdx];
        bool canEvolve = evolutionAllowed && (player.EvolutionStones >= PlayerState.EvolutionRequired);

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
        modalRt.sizeDelta = new Vector2(BattleModalW, BattleModalH);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(modalWindow.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f, 80f);
        titleRt.anchoredPosition = new Vector2(0f, BattleModalTitleY);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        if (playerIdx != 0)
        {
            titleTxt.text = "OPPONENT CREATURE";
        }
        else
        {
            titleTxt.text = canEvolve ? "EVOLUTION AVAILABLE" : "EVOLUTION LOCKED";
        }
        StyleBattlePopupTitle(titleTxt);
        titleTxt.fontSize = 52f;
        if (messageText != null) titleTxt.font = messageText.font;

        // 4. Creature Avatar
        GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(modalWindow.transform, false);
        RectTransform avatarRt = avatarGo.GetComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0.5f, 1f);
        avatarRt.anchorMax = new Vector2(0.5f, 1f);
        avatarRt.pivot = new Vector2(0.5f, 1f);
        avatarRt.sizeDelta = new Vector2(ManualEvoAvatarSize, ManualEvoAvatarSize);
        avatarRt.anchoredPosition = new Vector2(0f, -325f);

        Image avatarImg = avatarGo.GetComponent<Image>();
        avatarImg.sprite = poke.Avatar;
        avatarImg.preserveAspect = true;
        avatarImg.raycastTarget = false;

        // 5. Creature stats + evolve prompt
        GameObject descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        descGo.transform.SetParent(modalWindow.transform, false);
        RectTransform descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.5f, 0.5f);
        descRt.anchorMax = new Vector2(0.5f, 0.5f);
        descRt.pivot = new Vector2(0.5f, 0.5f);
        descRt.sizeDelta = new Vector2(700f, 380f);
        descRt.anchoredPosition = new Vector2(0f, -175f);

        TMPro.TextMeshProUGUI descTxt = descGo.GetComponent<TMPro.TextMeshProUGUI>();
        string detailBody = BuildCreatureDetailBody(poke);
        if (playerIdx != 0)
        {
            descTxt.text = $"{detailBody}\n\nRequires <color={BattleHighlightGold}><b>{PlayerState.EvolutionRequired}</b></color> Charry gems to evolve.\nOpponent has <color={BattleHighlightGold}><b>{player.EvolutionStones}/{PlayerState.EvolutionRequired}</b></color> gems.";
        }
        else
        {
            if (canEvolve)
                descTxt.text = $"{detailBody}\n\nDo you want to evolve <b>{poke.Name}</b>?\n(Costs 1 Move and resets Charry Gems)";
            else
                descTxt.text = $"{detailBody}\n\nRequires <color={BattleHighlightGold}><b>{PlayerState.EvolutionRequired}</b></color> Charry gems to evolve.\nYou have <color={BattleHighlightGold}><b>{player.EvolutionStones}/{PlayerState.EvolutionRequired}</b></color>.";
        }
        StyleBattlePopupBody(descTxt, 32f);
        descTxt.paragraphSpacing = 10f;
        descTxt.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
        if (messageText != null) descTxt.font = messageText.font;

        // 6. Action buttons
        if (canEvolve)
        {
            Sprite[] newButtonSprites = Resources.LoadAll<Sprite>("buttons/new-buttons");
            Sprite yesBtnSprite = System.Array.Find(popupSprites, s => s.name == "popup_0");
            if (yesBtnSprite == null) yesBtnSprite = System.Array.Find(newButtonSprites, s => s.name == "new-buttons_8");
            Sprite noBtnSprite = System.Array.Find(popupSprites, s => s.name == "popup_1");
            if (noBtnSprite == null) noBtnSprite = System.Array.Find(newButtonSprites, s => s.name == "new-buttons_9");

            // Yes Button
            GameObject evolveGo = new GameObject("YesButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            evolveGo.transform.SetParent(modalWindow.transform, false);
            RectTransform evolveRt = evolveGo.GetComponent<RectTransform>();
            evolveRt.anchorMin = new Vector2(0.5f, 0f);
            evolveRt.anchorMax = new Vector2(0.5f, 0f);
            evolveRt.pivot = new Vector2(0.5f, 0f);
            evolveRt.sizeDelta = new Vector2(300f, 200f);
            evolveRt.anchoredPosition = new Vector2(-160f, 150f);

            Image evolveImg = evolveGo.GetComponent<Image>();
            if (yesBtnSprite != null)
            {
                evolveImg.sprite = yesBtnSprite;
                evolveImg.type = Image.Type.Simple;
            }
            evolveImg.color = Color.white;

            evolveGo.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                Destroy(_manualEvoPopupInstance);
                BoardManager.GetInstance().TryManualEvolve(playerIdx, poke);
            });

            // No Button
            GameObject noBtnGo = new GameObject("NoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            noBtnGo.transform.SetParent(modalWindow.transform, false);
            RectTransform noRt = noBtnGo.GetComponent<RectTransform>();
            noRt.anchorMin = new Vector2(0.5f, 0f);
            noRt.anchorMax = new Vector2(0.5f, 0f);
            noRt.pivot = new Vector2(0.5f, 0f);
            noRt.sizeDelta = new Vector2(300f, 200f);
            noRt.anchoredPosition = new Vector2(160f, 150f);

            Image noImg = noBtnGo.GetComponent<Image>();
            if (noBtnSprite != null)
            {
                noImg.sprite = noBtnSprite;
                noImg.type = Image.Type.Simple;
            }
            noImg.color = Color.white;

            noBtnGo.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                Destroy(_manualEvoPopupInstance);
            });
        }
        else
        {
            var closeBtn = CreateBattleOkButton(modalWindow.transform, new Vector2(130f, 150f), () =>
            {
                Destroy(_manualEvoPopupInstance);
            });
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.sizeDelta = new Vector2(300f, 200f);
            closeRt.anchoredPosition = new Vector2(0f, 150f);
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

    private void UpdateCreatureNameRow(int badgeIndex, TMPro.TextMeshProUGUI nameLabel, CreatureState poke)
    {
        if (nameLabel == null) return;

        string displayName = poke.Name;
        if (nameLabel.text != displayName)
            nameLabel.text = displayName;

        SetCreatureEvoBadgeVisible(badgeIndex, poke.IsEvolved);
    }

    private Sprite GetEvolutionBadgeSprite()
    {
        if (evolutionBadgeSprite != null) return evolutionBadgeSprite;
        if (_cachedEvolutionBadgeSprite != null) return _cachedEvolutionBadgeSprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>("UI/UI-pack_Sprite_1");
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i].name == "UI-pack_Sprite_1_5")
                {
                    _cachedEvolutionBadgeSprite = sprites[i];
                    evolutionBadgeSprite = _cachedEvolutionBadgeSprite;
                    return _cachedEvolutionBadgeSprite;
                }
            }
        }

        UnityEngine.Debug.LogWarning("[BoardInputHandler] Evolution badge sprite UI-pack_Sprite_1_5 not found.");
        return null;
    }

    private static void ApplyEvoBadgeLayout(RectTransform badgeRt)
    {
        const float size = 20f;
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.sizeDelta = new Vector2(size, size);
        badgeRt.anchoredPosition = Vector2.zero;
    }

    private void EnsureCreatureEvoBadges()
    {
        Sprite badgeSprite = GetEvolutionBadgeSprite();
        if (badgeSprite == null) return;

        TMPro.TextMeshProUGUI[] nameLabels = { p1Poke1Name, p1Poke2Name, p2Poke1Name, p2Poke2Name };
        for (int i = 0; i < nameLabels.Length; i++)
        {
            TMPro.TextMeshProUGUI nameLabel = nameLabels[i];
            if (nameLabel == null) continue;

            Transform existing = nameLabel.transform.Find("EvoBadge");
            Image badgeImg;
            if (existing != null)
            {
                badgeImg = existing.GetComponent<Image>();
                ApplyEvoBadgeLayout(existing.GetComponent<RectTransform>());
            }
            else
            {
                GameObject badgeGo = new GameObject("EvoBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.transform.SetParent(nameLabel.transform, false);
                badgeGo.transform.SetAsFirstSibling();

                RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
                ApplyEvoBadgeLayout(badgeRt);

                badgeImg = badgeGo.GetComponent<Image>();
                badgeImg.sprite = badgeSprite;
                badgeImg.preserveAspect = true;
                badgeImg.color = Color.white;
                badgeImg.raycastTarget = false;
                badgeGo.SetActive(false);
            }

            _creatureEvoBadges[i] = badgeImg;
        }

        _creatureEvoBadgesReady = true;
    }

    private void SetCreatureEvoBadgeVisible(int badgeIndex, bool visible)
    {
        EnsureCreatureEvoBadges();
        if (badgeIndex < 0 || badgeIndex >= _creatureEvoBadges.Length) return;
        Image badge = _creatureEvoBadges[badgeIndex];
        if (badge != null)
            badge.gameObject.SetActive(visible);
    }

    private string GetCategoryName(GemType type)
    {
        return type switch
        {
            GemType.Fire => "Fire Category",
            GemType.Water => "Water Category",
            GemType.Nature => "Nature Category",
            GemType.Electric => "Storm Category",
            GemType.Psychic => "Psychic Category",
            GemType.Healing => "Light Category",
            _ => type.ToString()
        };
    }

    private static string GetGemTypeHexColor(GemType type)
    {
        return type switch
        {
            GemType.Fire     => "#FF5522",
            GemType.Water    => "#22AAFF",
            GemType.Nature   => "#33DD33",
            GemType.Electric => "#FFCC00",
            GemType.Psychic  => "#CC44FF",
            GemType.Healing  => "#FF88BB",
            _                => "#FFFFFF"
        };
    }

    private static void StyleBattlePopupTitle(TMPro.TextMeshProUGUI txt)
    {
        if (txt == null) return;
        txt.fontSize = 50f;
        txt.fontStyle = TMPro.FontStyles.Bold;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = BattlePopupTitleColor;
    }

    private static void StyleBattlePopupBody(TMPro.TextMeshProUGUI txt, float fontSize = 36f)
    {
        if (txt == null) return;
        txt.fontSize = fontSize;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.enableWordWrapping = true;
        txt.lineSpacing = 0f;
        txt.paragraphSpacing = 0f;
        txt.color = BattlePopupBodyColor;
    }

    private static Sprite GetBattleOkButtonSprite()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("buttons/login-logout");
        return System.Array.Find(sprites, s => s.name == "login-logout_4");
    }

    private static void ApplyBattleOkButtonImage(Image img)
    {
        if (img == null) return;
        Sprite sprite = GetBattleOkButtonSprite();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }
        img.color = Color.white;
    }

    private static UnityEngine.UI.Button CreateBattleOkButton(Transform parent, Vector2 anchoredPos, Action onClick)
    {
        var go = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(BattleModalOkBtnW, BattleModalOkBtnH);
        rt.anchoredPosition = anchoredPos;

        ApplyBattleOkButtonImage(go.GetComponent<Image>());
        var btn = go.GetComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    private string BuildCreatureDetailBody(CreatureState poke)
    {
        bool isHeal = poke.Type == GemType.Nature || poke.Type == GemType.Healing;
        string typeColor = GetGemTypeHexColor(poke.Type);
        string category = GetCategoryName(poke.Type);
        int normalPower = poke.BaseValue + poke.EvolutionDamageBonus;
        int afterEvolution = poke.IsEvolved ? normalPower : normalPower + 5;
        string powerNoun = isHeal ? "healing power" : "attack power";
        string powerWord = isHeal ? "healing" : "attack";

        var board = BoardManager.GetInstance();
        AttackRule rule = board != null ? board.GetAttackRule(poke.Type) : null;
        string abilityName = rule != null ? rule.AttackName : "Ability";
        string abilityDesc = rule != null ? rule.EffectDescription : "No special effect recorded.";
        int abilityDamage = rule != null ? rule.Damage : 0;
        int stonesRequired = rule != null ? rule.StonesRequired : 0;

        string creatureInfo =
            $"<b>{poke.Name}</b> is a <color={typeColor}><b>{category}</b></color> creature who fights with matching element stones and currently brings <color={BattleHighlightGold}><b>{normalPower}</b></color> {powerNoun} into every clash.";

        string evolutionInfo = poke.IsEvolved
            ? $"This unit has already evolved, reaching <color={BattleHighlightGreen}><b>{afterEvolution}</b></color> {powerWord} strength, and can carry up to <color={BattleHighlightGold}><b>{poke.MaxEnergy}</b></color> energy gems before unleashing its full potential."
            : $"Should this creature evolve, its {powerWord} strength would rise to <color={BattleHighlightGreen}><b>{afterEvolution}</b></color>, and it can hold up to <color={BattleHighlightGold}><b>{poke.MaxEnergy}</b></color> energy gems while building power across the board.";

        string abilityAction = isHeal ? "restores" : "deals";
        string extraDetails =
            $"In battle it relies on <color={BattleHighlightGold}><b>{abilityName}</b></color>, which {abilityAction} <color={BattleHighlightGold}><b>{abilityDamage}</b></color> after <color={BattleHighlightGold}><b>{stonesRequired}</b></color> linked gems are gathered, <color={BattleHighlightGold}><b>{abilityDesc}</b></color>.";

        return $"{creatureInfo}\n\n{evolutionInfo}\n\n{extraDetails}";
    }
}
