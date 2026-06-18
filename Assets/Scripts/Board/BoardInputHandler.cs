using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class BoardInputHandler : MonoBehaviour
{
    [SerializeField] private Transform boardParent;
    [SerializeField] private float cellSize = 80f;
    [SerializeField] private float spacing = 4f;
    [SerializeField] private Color charryColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color borderColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private float borderWidth = 2f;
    [SerializeField] private Color boardBgColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);

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

    // Player UI Fields
    private GameObject playerUIPanel;
    private TMPro.TextMeshProUGUI p1NameText, p2NameText;
    private TMPro.TextMeshProUGUI p1MovesText, p2MovesText;
    private TMPro.TextMeshProUGUI p1HpText, p2HpText;
    private Image p1HpBar, p2HpBar;
    private Image p1HpBarTrailing, p2HpBarTrailing;

    // Pokemon 1 UI
    private Image p1Poke1Avatar, p2Poke1Avatar;
    private TMPro.TextMeshProUGUI p1Poke1Name, p2Poke1Name;
    private TMPro.TextMeshProUGUI p1Poke1EnergyText, p2Poke1EnergyText;
    private Image p1Poke1EnergyBar, p2Poke1EnergyBar;
    private Image p1Poke1Stone, p2Poke1Stone;

    // Pokemon 2 UI
    private Image p1Poke2Avatar, p2Poke2Avatar;
    private TMPro.TextMeshProUGUI p1Poke2Name, p2Poke2Name;
    private TMPro.TextMeshProUGUI p1Poke2EnergyText, p2Poke2EnergyText;
    private Image p1Poke2EnergyBar, p2Poke2EnergyBar;
    private Image p1Poke2Stone, p2Poke2Stone;

    private TMPro.TextMeshProUGUI messageText;
    private Image p1PanelBg, p2PanelBg;

    // --- Pip charge-bar system ---
    // Layout: index = playerIdx * 2 + pokemonIdx  (0=P1/Poke1, 1=P1/Poke2, 2=P2/Poke1, 3=P2/Poke2)
    private Image[][] pokemonPips          = new Image[4][];
    private TMPro.TextMeshProUGUI[] pokemonAttackLabels = new TMPro.TextMeshProUGUI[4];
    private Transform[] pokemonEnergyBgTransforms       = new Transform[4];
    private int _pokemonRowIndex; // incremented inside CreatePokemonRow

    private void Start()
    {
        // Allow sprites to be assigned in the inspector. If any slots are empty,
        // attempt to fill them from Resources/Gems/element_block_1..6.
        int expected = 6;
        if (gemSprites == null || gemSprites.Length != expected)
            gemSprites = new Sprite[expected];

        bool anyLoaded = false;
        for (int i = 0; i < expected; i++)
        {
            if (gemSprites[i] == null)
                gemSprites[i] = Resources.Load<Sprite>("Gems/element_block_" + (i + 1));
            if (gemSprites[i] != null)
                anyLoaded = true;
        }

        if (!anyLoaded)
        {
            UnityEngine.Debug.LogWarning("[BoardInputHandler] No gem sprites found. " +
                "Place gem sprites in Assets/Resources/Gems/ named element_block_1..element_block_6 " +
                "or assign the `gemSprites` array in the inspector.");
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

        // ── Destroy old board objects before recreating ────────────────────────
        if (boardParent != null)
        {
            for (int i = boardParent.childCount - 1; i >= 0; i--)
                Destroy(boardParent.GetChild(i).gameObject);
        }
        if (playerUIPanel != null)
        {
            Destroy(playerUIPanel);
            playerUIPanel = null;
        }
        // Reset pip tracking arrays so InitPips rebuilds them cleanly.
        pokemonPips               = new Image[4][];
        pokemonAttackLabels       = new TMPro.TextMeshProUGUI[4];
        pokemonEnergyBgTransforms = new Transform[4];
        // ────────────────────────────────────────────────────────────────────────

        CreateBackground();
        CreateGrid();
        _pokemonRowIndex = 0;
        CreatePlayerUI();
        RefreshBoard();
        InitPips();   // build segmented pip bars now that Pokémon types are known
        isInitialized = true;
    }

    // Returns true only if the Image component still refers to a live (non-destroyed) object.
    // Unity overrides == null for UnityEngine.Object, so this catches destroyed objects too.
    private static bool IsAlive(Image img)   => img != null && img;
    private static bool IsAlive(RectTransform rt) => rt != null && rt;

    private void CreateBackground()
    {
        GameObject bg = new GameObject("BoardBackground", typeof(Image));
        bg.transform.SetParent(boardParent, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;
        bgRt.sizeDelta = new Vector2(totalW + cellSize * 0.5f, totalH + cellSize * 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        Image bgImg = bg.GetComponent<Image>();
        bgImg.color = boardBgColor;
        bg.transform.SetSiblingIndex(0);
    }

    private void CreateGrid()
    {
        gemImages = new Image[GridModel.ROWS, GridModel.COLS];
        gemRects = new RectTransform[GridModel.ROWS, GridModel.COLS];

        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;

        boardRect = boardParent.GetComponent<RectTransform>();
        boardRect.sizeDelta = new Vector2(totalW, totalH);

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                GameObject cell = new GameObject("Gem", typeof(Image));
                cell.transform.SetParent(boardParent, false);
                RectTransform rt = cell.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(
                    c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                    -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
                );

                Image img = cell.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = true;

                Outline outline = cell.AddComponent<Outline>();
                outline.effectColor = borderColor;
                outline.effectDistance = new Vector2(borderWidth, borderWidth);

                gemImages[r, c] = img;
                gemRects[r, c] = rt;

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
        if (gemImages == null) return null;
        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                if (gemImages[r, c] != null && gemImages[r, c].gameObject == go)
                {
                    return new Vector2Int(c, r);
                }
            }
        }
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
        if (!isInitialized) return;
        if (isAnimating) return;
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
        if (delta.magnitude < 40f) return; // swipe threshold

        Vector2Int from = firstSelection.Value;
        Vector2Int to = from;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            to.x += delta.x > 0 ? 1 : -1;
        else
            to.y += delta.y > 0 ? 1 : -1;

        if (!GridModel.IsValidPosition(to.y, to.x) || to == from) return;

        gemRects[from.y, from.x].DOScale(1f, 0.1f);
        firstSelection = null;

        StartCoroutine(AnimateSwap(from, to));
    }

    private void OnGemDragEnd(PointerEventData data)
    {
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
        yield return new WaitForSeconds(0.2f);

        Image tmpImg = gemImages[from.y, from.x];
        gemImages[from.y, from.x] = gemImages[to.y, to.x];
        gemImages[to.y, to.x] = tmpImg;

        RectTransform tmpRt = gemRects[from.y, from.x];
        gemRects[from.y, from.x] = gemRects[to.y, to.x];
        gemRects[to.y, to.x] = tmpRt;

        bool valid = board.TrySwap(from, to);

        if (!valid)
        {
            gemRects[to.y, to.x].DOAnchorPos(posA, 0.2f).SetEase(Ease.InOutQuad);
            gemRects[from.y, from.x].DOAnchorPos(posB, 0.2f).SetEase(Ease.InOutQuad);
            yield return new WaitForSeconds(0.2f);

            Image tmpImg2 = gemImages[from.y, from.x];
            gemImages[from.y, from.x] = gemImages[to.y, to.x];
            gemImages[to.y, to.x] = tmpImg2;

            RectTransform tmpRt2 = gemRects[from.y, from.x];
            gemRects[from.y, from.x] = gemRects[to.y, to.x];
            gemRects[to.y, to.x] = tmpRt2;

            rtA.anchoredPosition = posA;
            rtB.anchoredPosition = posB;

            gemRects[to.y, to.x].DOShakePosition(0.3f, 5f);
        }

        isAnimating = false;
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

    private void OnCascadeComplete(List<Vector2Int> newStonePositions)
    {
        StartCoroutine(AnimateCascade(newStonePositions));
    }

    private IEnumerator AnimateCascade(List<Vector2Int> newStonePositions)
    {
        // Guard: arrays may have been cleared by OnBoardInit during a game reset.
        if (gemImages == null || gemRects == null) yield break;

        HashSet<Vector2Int> newSet = new HashSet<Vector2Int>(newStonePositions);

        BoardManager board = BoardManager.GetInstance();
        if (board?.Grid == null) yield break;

        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;
        float dropDistance = cellSize * 2.0f;

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

                Vector2 targetPos = new Vector2(
                    c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                    -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
                );

                bool isCharry  = idx == (int)GemType.Charry;
                bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
                bool isNew     = newSet.Contains(new Vector2Int(c, r));

                if (isCharry)
                {
                    img.sprite = null; img.color = Color.clear;
                    if (cg != null) cg.alpha = 0f;
                    rt2.anchoredPosition = targetPos;
                    rt2.localScale = Vector3.zero;
                    img.raycastTarget = false;
                }
                else if (isNew)
                {
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    if (cg != null) cg.alpha = 0f;
                    rt2.anchoredPosition = targetPos + new Vector2(0f, dropDistance);
                    rt2.localScale = Vector3.one * 0.6f;
                    img.raycastTarget = false;
                }
                else
                {
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    if (cg != null) cg.alpha = 1f;
                    rt2.anchoredPosition = targetPos;
                    rt2.localScale = Vector3.one;
                    img.raycastTarget = true;
                }
            }
        }

        yield return null; // let Pass-1 render before tweens start

        // ── PASS 2: guard again — board may have reset during the yield ──────────
        if (gemImages == null || gemRects == null) yield break;

        // Animate only brand-new stones dropping in from the top
        foreach (Vector2Int cell in newStonePositions)
        {
            if (gemImages == null || gemRects == null) yield break;

            int r = cell.y;
            int c = cell.x;

            Image         img2 = gemImages[r, c];
            RectTransform rt2b = gemRects[r, c];
            if (!IsAlive(img2) || !IsAlive(rt2b)) continue;

            GemType gem2 = board.Grid.Grid[r, c];
            if (gem2 == GemType.Charry) continue;

            Vector2 targetPos2 = new Vector2(
                c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
            );

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

        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;

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
                    c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                    -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
                );

                if (isCharry)
                {
                    img.sprite = null;
                    img.color  = Color.clear;
                    if (canvasGroup != null) canvasGroup.alpha = 0f;
                    rt.localScale = Vector3.zero;
                    img.raycastTarget = false;
                }
                else
                {
                    if (canvasGroup != null) canvasGroup.alpha = 1f;
                    rt.localScale = Vector3.one;

                    bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
                    img.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
                    img.color  = hasSprite ? Color.white : GetGemColor(gem);
                    img.raycastTarget = true;
                }
            }
        }
    }

    private void CreatePlayerUI()
    {
        Transform canvasTransform = boardParent.parent;
        if (canvasTransform == null) canvasTransform = boardParent;

        playerUIPanel = new GameObject("PlayerUIPanel", typeof(RectTransform));
        playerUIPanel.transform.SetParent(canvasTransform, false);
        RectTransform panelRt = playerUIPanel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(boardRect.sizeDelta.x + 40f, 200f);
        panelRt.anchoredPosition = new Vector2(boardRect.anchoredPosition.x, boardRect.anchoredPosition.y + boardRect.sizeDelta.y / 2f + 120f);

        p1PanelBg = CreatePlayerCard(
            playerUIPanel.transform, 
            -boardRect.sizeDelta.x / 4f - 10f, 
            out p1NameText, 
            out p1MovesText, 
            out p1HpText, 
            out p1HpBar,
            out p1HpBarTrailing,
            out p1Poke1Avatar, 
            out p1Poke1Name, 
            out p1Poke1EnergyText, 
            out p1Poke1EnergyBar,
            out p1Poke1Stone,
            out p1Poke2Avatar, 
            out p1Poke2Name, 
            out p1Poke2EnergyText, 
            out p1Poke2EnergyBar,
            out p1Poke2Stone,
            true
        );

        p2PanelBg = CreatePlayerCard(
            playerUIPanel.transform, 
            boardRect.sizeDelta.x / 4f + 10f, 
            out p2NameText, 
            out p2MovesText, 
            out p2HpText, 
            out p2HpBar,
            out p2HpBarTrailing,
            out p2Poke1Avatar, 
            out p2Poke1Name, 
            out p2Poke1EnergyText, 
            out p2Poke1EnergyBar,
            out p2Poke1Stone,
            out p2Poke2Avatar, 
            out p2Poke2Name, 
            out p2Poke2EnergyText, 
            out p2Poke2EnergyBar,
            out p2Poke2Stone,
            false
        );

        GameObject msgGo = new GameObject("MessageText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        msgGo.transform.SetParent(playerUIPanel.transform, false);
        RectTransform msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.sizeDelta = new Vector2(boardRect.sizeDelta.x, 60f);
        msgRt.anchoredPosition = new Vector2(0f, -110f);
        messageText = msgGo.GetComponent<TMPro.TextMeshProUGUI>();
        messageText.alignment = TMPro.TextAlignmentOptions.Center;
        messageText.fontSize = 28f;
        messageText.fontStyle = TMPro.FontStyles.Bold;
        messageText.color = new Color(1f, 0.9f, 0.2f);
        
        Outline msgOutline = msgGo.AddComponent<Outline>();
        msgOutline.effectColor = new Color(0, 0, 0, 0.8f);
        msgOutline.effectDistance = new Vector2(3f, -3f);

        UpdatePlayerUI();
    }

    private Image CreatePlayerCard(
        Transform parent, 
        float posX, 
        out TMPro.TextMeshProUGUI nameTxt, 
        out TMPro.TextMeshProUGUI movesTxt, 
        out TMPro.TextMeshProUGUI hpTxt, 
        out Image hpBar,
        out Image hpBarTrailing,
        out Image p1Av, 
        out TMPro.TextMeshProUGUI p1Name, 
        out TMPro.TextMeshProUGUI p1EnergyTxt, 
        out Image p1EnergyBar,
        out Image p1Stone,
        out Image p2Av, 
        out TMPro.TextMeshProUGUI p2Name, 
        out TMPro.TextMeshProUGUI p2EnergyTxt, 
        out Image p2EnergyBar,
        out Image p2Stone,
        bool isP1
    )
    {
        GameObject card = new GameObject(isP1 ? "P1Card" : "P2Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(boardRect.sizeDelta.x / 2f - 15f, 190f);
        cardRt.anchoredPosition = new Vector2(posX, 0f);
        Image bgImg = card.GetComponent<Image>();
        bgImg.color = new Color(0.16f, 0.16f, 0.22f, 0.9f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = isP1 ? new Color(1f, 0.35f, 0.2f, 0.5f) : new Color(0.2f, 0.6f, 1f, 0.5f);
        outline.effectDistance = new Vector2(2f, 2f);

        Shadow shadow = card.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.6f);
        shadow.effectDistance = new Vector2(4f, -4f);

        GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        nameGo.transform.SetParent(card.transform, false);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(100f, 25f);
        nameRt.anchoredPosition = new Vector2(-cardRt.sizeDelta.x / 2f + 55f, 75f);
        nameTxt = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
        nameTxt.alignment = TMPro.TextAlignmentOptions.Left;
        nameTxt.fontSize = 14f;
        nameTxt.fontStyle = TMPro.FontStyles.Bold;
        nameTxt.color = Color.white;

        GameObject hpBgGo = new GameObject("HPBarBg", typeof(RectTransform), typeof(Image));
        hpBgGo.transform.SetParent(card.transform, false);
        RectTransform hpBgRt = hpBgGo.GetComponent<RectTransform>();
        hpBgRt.sizeDelta = new Vector2(100f, 8f);
        hpBgRt.anchoredPosition = new Vector2(cardRt.sizeDelta.x / 2f - 55f, 75f);
        Image hpBgImg = hpBgGo.GetComponent<Image>();
        hpBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // --- Trailing damage bar ---
        GameObject hpTrailingGo = new GameObject("HPBarTrailing", typeof(RectTransform), typeof(Image));
        hpTrailingGo.transform.SetParent(hpBgGo.transform, false);
        RectTransform hpTrailingRt = hpTrailingGo.GetComponent<RectTransform>();
        hpTrailingRt.sizeDelta = new Vector2(100f, 8f);
        hpTrailingRt.anchoredPosition = Vector2.zero;
        hpBarTrailing = hpTrailingGo.GetComponent<Image>();
        hpBarTrailing.color = new Color(0.9f, 0.25f, 0.2f); // Red catch-up line
        hpBarTrailing.type = Image.Type.Filled;
        hpBarTrailing.fillMethod = Image.FillMethod.Horizontal;
        hpBarTrailing.fillAmount = 1f;

        // --- Main HP bar ---
        GameObject hpFillGo = new GameObject("HPBarFill", typeof(RectTransform), typeof(Image));
        hpFillGo.transform.SetParent(hpBgGo.transform, false);
        RectTransform hpFillRt = hpFillGo.GetComponent<RectTransform>();
        hpFillRt.sizeDelta = new Vector2(100f, 8f);
        hpFillRt.anchoredPosition = Vector2.zero;
        hpBar = hpFillGo.GetComponent<Image>();
        hpBar.color = new Color(0.2f, 0.8f, 0.3f);
        hpBar.type = Image.Type.Filled;
        hpBar.fillMethod = Image.FillMethod.Horizontal;
        hpBar.fillAmount = 1f;

        GameObject hpTextGo = new GameObject("HPText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        hpTextGo.transform.SetParent(card.transform, false);
        RectTransform hpTextRt = hpTextGo.GetComponent<RectTransform>();
        hpTextRt.sizeDelta = new Vector2(100f, 15f);
        hpTextRt.anchoredPosition = new Vector2(cardRt.sizeDelta.x / 2f - 55f, 87f);
        hpTxt = hpTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        hpTxt.alignment = TMPro.TextAlignmentOptions.Center;
        hpTxt.fontSize = 9f;
        hpTxt.color = new Color(0.9f, 0.9f, 0.9f);

        GameObject movesGo = new GameObject("MovesText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        movesGo.transform.SetParent(card.transform, false);
        RectTransform movesRt = movesGo.GetComponent<RectTransform>();
        movesRt.sizeDelta = new Vector2(100f, 18f);
        movesRt.anchoredPosition = new Vector2(cardRt.sizeDelta.x / 2f - 55f, 55f);
        movesTxt = movesGo.GetComponent<TMPro.TextMeshProUGUI>();
        movesTxt.alignment = TMPro.TextAlignmentOptions.Right;
        movesTxt.fontSize = 11f;
        movesTxt.color = new Color(0.9f, 0.9f, 0.5f);

        CreatePokemonRow(card.transform, 10f, out p1Av, out p1Name, out p1EnergyTxt, out p1EnergyBar, out p1Stone);
        CreatePokemonRow(card.transform, -45f, out p2Av, out p2Name, out p2EnergyTxt, out p2EnergyBar, out p2Stone);

        return bgImg;
    }

    private void CreatePokemonRow(Transform parent, float posY, out Image av, out TMPro.TextMeshProUGUI nameTxt, out TMPro.TextMeshProUGUI energyTxt, out Image energyBar, out Image stoneImg)
    {
        int rowIdx = _pokemonRowIndex++; // capture slot index before incrementing

        GameObject row = new GameObject("PokemonRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(boardRect.sizeDelta.x / 2f - 30f, 62f); // taller to fit attack label
        rowRt.anchoredPosition = new Vector2(0f, posY);

        // --- Row Background (soft highlight) ---
        GameObject rowBgGo = new GameObject("RowBg", typeof(RectTransform), typeof(Image));
        rowBgGo.transform.SetParent(row.transform, false);
        RectTransform rowBgRt = rowBgGo.GetComponent<RectTransform>();
        rowBgRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, rowRt.sizeDelta.y);
        rowBgRt.anchoredPosition = Vector2.zero;
        Image rowBgImg = rowBgGo.GetComponent<Image>();
        rowBgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.4f);

        // --- Avatar Frame ---
        GameObject avBgGo = new GameObject("AvatarBg", typeof(RectTransform), typeof(Image));
        avBgGo.transform.SetParent(row.transform, false);
        RectTransform avBgRt = avBgGo.GetComponent<RectTransform>();
        avBgRt.sizeDelta = new Vector2(44f, 44f);
        avBgRt.anchoredPosition = new Vector2(-rowRt.sizeDelta.x / 2f + 25f, 4f);
        Image avBgImg = avBgGo.GetComponent<Image>();
        avBgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        Shadow avShadow = avBgGo.AddComponent<Shadow>();
        avShadow.effectColor = new Color(0, 0, 0, 0.8f);
        avShadow.effectDistance = new Vector2(2f, -2f);

        // --- Avatar ---
        GameObject avGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avGo.transform.SetParent(row.transform, false);
        RectTransform avRt = avGo.GetComponent<RectTransform>();
        avRt.sizeDelta = new Vector2(40f, 40f);
        avRt.anchoredPosition = new Vector2(-rowRt.sizeDelta.x / 2f + 25f, 4f);
        av = avGo.GetComponent<Image>();
        av.preserveAspect = true;

        // --- Pokemon name ---
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        nameGo.transform.SetParent(row.transform, false);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(90f, 20f);
        nameRt.anchoredPosition = new Vector2(-rowRt.sizeDelta.x / 2f + 95f, 16f);
        nameTxt = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
        nameTxt.alignment = TMPro.TextAlignmentOptions.Left;
        nameTxt.fontSize = 11f;
        nameTxt.fontStyle = TMPro.FontStyles.Bold;
        nameTxt.color = Color.white;

        // --- Stone Icon ---
        GameObject stoneGo = new GameObject("StoneImage", typeof(RectTransform), typeof(Image));
        stoneGo.transform.SetParent(row.transform, false);
        RectTransform stoneRt = stoneGo.GetComponent<RectTransform>();
        stoneRt.sizeDelta = new Vector2(16f, 16f);
        stoneRt.anchoredPosition = new Vector2(-rowRt.sizeDelta.x / 2f + 145f, 16f);
        stoneImg = stoneGo.GetComponent<Image>();
        stoneImg.preserveAspect = true;

        // --- Energy pip bar background (pips built later in InitPips) ---
        GameObject energyBgGo = new GameObject("EnergyBg", typeof(RectTransform), typeof(Image));
        energyBgGo.transform.SetParent(row.transform, false);
        RectTransform energyBgRt = energyBgGo.GetComponent<RectTransform>();
        energyBgRt.sizeDelta = new Vector2(80f, 10f);
        energyBgRt.anchoredPosition = new Vector2(rowRt.sizeDelta.x / 2f - 45f, 0f);
        Image energyBgImg = energyBgGo.GetComponent<Image>();
        energyBgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        // Store transform so InitPips can parent pip images here
        pokemonEnergyBgTransforms[rowIdx] = energyBgGo.transform;

        // Hidden smooth fill (kept as out param to avoid breaking callers; pips replace it visually)
        GameObject energyFillGo = new GameObject("EnergyFill", typeof(RectTransform), typeof(Image));
        energyFillGo.transform.SetParent(energyBgGo.transform, false);
        RectTransform energyFillRt = energyFillGo.GetComponent<RectTransform>();
        energyFillRt.sizeDelta = new Vector2(80f, 10f);
        energyFillRt.anchoredPosition = Vector2.zero;
        energyBar = energyFillGo.GetComponent<Image>();
        energyBar.color = Color.clear; // hidden; pips provide the visual
        energyBar.type = Image.Type.Filled;
        energyBar.fillMethod = Image.FillMethod.Horizontal;
        energyBar.fillAmount = 0f;

        // --- Current/Max energy counter ---
        GameObject energyTextGo = new GameObject("EnergyText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        energyTextGo.transform.SetParent(row.transform, false);
        RectTransform energyTextRt = energyTextGo.GetComponent<RectTransform>();
        energyTextRt.sizeDelta = new Vector2(90f, 15f);
        energyTextRt.anchoredPosition = new Vector2(-rowRt.sizeDelta.x / 2f + 95f, 0f);
        energyTxt = energyTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        energyTxt.alignment = TMPro.TextAlignmentOptions.Left;
        energyTxt.fontSize = 9f;
        energyTxt.color = new Color(0.9f, 0.7f, 0.4f);

        // --- Attack label: "Collect N Type → AttackName" (populated in InitPips) ---
        GameObject attackLabelGo = new GameObject("AttackLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        attackLabelGo.transform.SetParent(row.transform, false);
        RectTransform attackLabelRt = attackLabelGo.GetComponent<RectTransform>();
        attackLabelRt.sizeDelta = new Vector2(rowRt.sizeDelta.x - 10f, 14f);
        attackLabelRt.anchoredPosition = new Vector2(0f, -20f);
        TMPro.TextMeshProUGUI attackLabelTxt = attackLabelGo.GetComponent<TMPro.TextMeshProUGUI>();
        attackLabelTxt.alignment = TMPro.TextAlignmentOptions.Center;
        attackLabelTxt.fontSize = 8f;
        attackLabelTxt.color = new Color(0.75f, 0.75f, 0.85f, 0.9f);
        attackLabelTxt.text = "";
        pokemonAttackLabels[rowIdx] = attackLabelTxt;
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

        // P1 Main Info
        p1NameText.text = board.Players[0].Name;
        p1MovesText.text = "Moves: " + GetMovesIndicator(board.Players[0].MovesRemaining);
        p1HpText.text = board.Players[0].HP + " / " + board.Players[0].MaxHP + (board.Players[0].Shield > 0 ? " [" + board.Players[0].Shield + " SHIELD]" : "");
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
        p1Poke1Name.text = p1Poke1.Name + " (" + p1Poke1.Type + ")";
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
        p1Poke2Name.text = p1Poke2.Name + " (" + p1Poke2.Type + ")";
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
        p2MovesText.text = "Moves: " + GetMovesIndicator(board.Players[1].MovesRemaining);
        p2HpText.text = board.Players[1].HP + " / " + board.Players[1].MaxHP + (board.Players[1].Shield > 0 ? " [" + board.Players[1].Shield + " SHIELD]" : "");
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
        p2Poke1Name.text = p2Poke1.Name + " (" + p2Poke1.Type + ")";
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
        p2Poke2Name.text = p2Poke2.Name + " (" + p2Poke2.Type + ")";
        p2Poke2EnergyText.text = "Energy: " + p2Poke2.CurrentEnergy + "/" + p2Poke2.MaxEnergy;
        if (p2Poke2EnergyBar != null && p2Poke2EnergyBar) p2Poke2EnergyBar.fillAmount = (float)p2Poke2.CurrentEnergy / p2Poke2.MaxEnergy;
        if (IsAlive(p2Poke2Stone))
        {
            int idx = (int)p2Poke2.Type;
            bool hasSprite = gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null;
            p2Poke2Stone.sprite = hasSprite ? gemSprites[idx] : fallbackGemSprite;
            p2Poke2Stone.color = hasSprite ? Color.white : GetGemColor(p2Poke2.Type);
        }

        Color activeColor = new Color(0.24f, 0.24f, 0.32f, 1f);
        Color inactiveColor = new Color(0.12f, 0.12f, 0.16f, 0.8f);

        if (board.ActivePlayerIndex == 0)
        {
            p1PanelBg.DOColor(activeColor, 0.3f);
            p2PanelBg.DOColor(inactiveColor, 0.3f);
            p1PanelBg.transform.DOScale(1.03f, 0.3f);
            p2PanelBg.transform.DOScale(0.97f, 0.3f);
        }
        else
        {
            p1PanelBg.DOColor(inactiveColor, 0.3f);
            p2PanelBg.DOColor(activeColor, 0.3f);
            p1PanelBg.transform.DOScale(0.97f, 0.3f);
            p2PanelBg.transform.DOScale(1.03f, 0.3f);
        }

        RefreshPips();
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
                int stonesRequired = (rule != null && rule.StonesRequired > 0)
                    ? rule.StonesRequired
                    : poke.MaxEnergy;
                int n = stonesRequired; // e.g. 6 for Fire, 4 for Water

                float barW   = 80f;
                float barH   = 8f;
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

                // Attack label: "Collect 6 Fire → Ember (15 dmg)"
                if (pokemonAttackLabels[idx] != null)
                {
                    string dmgPart = rule.Damage > 0
                        ? $" ({rule.Damage} dmg)"
                        : " (no dmg)";
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
                Color gemCol   = GetGemColor(poke.Type);
                Color emptyCol = new Color(0.18f, 0.18f, 0.18f, 0.7f);

                AttackRule rule = board.GetAttackRule(poke.Type);
                int stonesRequired = (rule != null && rule.StonesRequired > 0)
                    ? rule.StonesRequired
                    : poke.MaxEnergy;

                for (int i = 0; i < pips.Length; i++)
                {
                    // Guard destroyed pip Images (can happen during board reset)
                    if (!IsAlive(pips[i])) continue;

                    bool filled = i < poke.CurrentEnergy;
                    pips[i].color = filled ? gemCol : emptyCol;
                    pips[i].transform.localScale = filled
                        ? new Vector3(1f, 1.15f, 1f)
                        : Vector3.one * 0.85f;
                }

                // Update the numeric energy label
                TMPro.TextMeshProUGUI label = (idx == 0) ? p1Poke1EnergyText
                                            : (idx == 1) ? p1Poke2EnergyText
                                            : (idx == 2) ? p2Poke1EnergyText
                                            :              p2Poke2EnergyText;
                if (label != null)
                    label.text = poke.CurrentEnergy + "/" + stonesRequired + " stones";
            }
        }
    }

    // Returns a vivid colour matching the gem type for pip/label colouring
    private Color GetGemColor(GemType type)
    {
        switch (type)
        {
            case GemType.Fire:     return new Color(1f,   0.38f, 0.12f);
            case GemType.Water:    return new Color(0.2f, 0.65f, 1f);
            case GemType.Nature:   return new Color(0.3f, 0.85f, 0.3f);
            case GemType.Electric: return new Color(1f,   0.92f, 0.1f);
            case GemType.Psychic:  return new Color(0.85f,0.3f,  1f);
            case GemType.Healing:  return new Color(1f,   0.55f, 0.8f);
            default:               return Color.white;
        }
    }

    private string GetMovesIndicator(int count)
    {
        string text = "";
        for (int i = 0; i < count; i++)
        {
            text += "●";
        }
        if (count == 0) text = "None";
        return text;
    }

    private void ShowMessage(string message)
    {
        // messageText is a child of playerUIPanel; bail if the panel has been destroyed.
        if (messageText == null || !messageText) return;
        
        // Reset base state
        messageText.transform.DOKill(complete: true);
        messageText.transform.localPosition = new Vector3(messageText.transform.localPosition.x, -110f, 0f);
        
        messageText.text = message;
        messageText.alpha = 0f;
        messageText.transform.localScale = Vector3.one * 1.5f;

        Sequence seq = DOTween.Sequence();
        seq.Join(messageText.DOFade(1f, 0.2f));
        seq.Join(messageText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        
        // Add a gentle continuous floating effect
        messageText.transform.DOBlendableLocalMoveBy(new Vector3(0, 8f, 0), 1.5f)
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
        BoardManager.OnMovesChanged  += UpdatePlayerUI;
        BoardManager.OnHPChanged     += UpdatePlayerUI;
        BoardManager.OnShowMessage   += ShowMessage;
        // Refresh pip bars whenever any Pokémon collects stones mid-turn
        BoardManager.OnEnergyChanged += RefreshPips;
    }

    private void OnDisable()
    {
        BoardManager.OnBoardInitialized -= OnBoardInit;
        BoardManager.OnMatchesFound     -= OnMatchesFound;
        BoardManager.OnCascadeComplete  -= OnCascadeComplete;

        BoardManager.OnTurnChanged   -= UpdatePlayerUI;
        BoardManager.OnMovesChanged  -= UpdatePlayerUI;
        BoardManager.OnHPChanged     -= UpdatePlayerUI;
        BoardManager.OnShowMessage   -= ShowMessage;
        BoardManager.OnEnergyChanged -= RefreshPips;
    }
}
