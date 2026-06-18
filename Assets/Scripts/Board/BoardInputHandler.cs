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

    private Sprite[] gemSprites;
    private Image[,] gemImages;
    private RectTransform[,] gemRects;
    private RectTransform boardRect;
    private Vector2Int? firstSelection;
    private bool isInitialized;
    private bool isAnimating;

    private float inactivityTime = 0f;
    private bool hintShowing = false;
    private Sequence hintSequence;

    private void Start()
    {
        gemSprites = new Sprite[6];
        for (int i = 0; i < 6; i++)
            gemSprites[i] = Resources.Load<Sprite>("Gems/element_block_" + (i + 1));

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
        if (isAnimating)
        {
            ResetInactivityTimer();
            return;
        }
        if (BoardManager.GetInstance().IsProcessing)
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

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                if (gemRects != null && gemRects[r, c] != null)
                {
                    gemRects[r, c].localScale = Vector3.one;
                }
            }
        }
    }

    private void OnBoardInit()
    {
        CreateBackground();
        CreateGrid();
        RefreshBoard();
        isInitialized = true;
    }

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
        foreach (Vector2Int pos in matches)
        {
            // Call the PlayDragAway animation
            gemImages[pos.y, pos.x].gameObject.PlayDragAway();
        }
    }

    private void OnCascadeComplete()
    {
        StartCoroutine(AnimateCascade());
    }

    private IEnumerator AnimateCascade()
    {
        BoardManager board = BoardManager.GetInstance();
        if (board.Grid == null) yield break;

        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                GemType gem = board.Grid.Grid[r, c];
                Image img = gemImages[r, c];
                int idx = (int)gem;

                // Reset position, scale, and alpha
                var canvasGroup = img.GetComponent<CanvasGroup>();
                if (canvasGroup != null) canvasGroup.alpha = 1f;

                gemRects[r, c].anchoredPosition = new Vector2(
                    c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                    -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
                );

                if (gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null)
                {
                    img.sprite = gemSprites[idx];
                    img.color = Color.white;
                }
                else
                {
                    img.sprite = null;
                    img.color = idx == 6 ? charryColor : Color.gray;
                }

                gemRects[r, c].localScale = Vector3.one * 0.5f;
                gemRects[r, c].DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetDelay(r * 0.02f);
            }
        }
    }

    private void RefreshBoard()
    {
        if (gemImages == null || BoardManager.GetInstance().Grid == null) return;

        float totalW = GridModel.COLS * cellSize + (GridModel.COLS - 1) * spacing;
        float totalH = GridModel.ROWS * cellSize + (GridModel.ROWS - 1) * spacing;

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                GemType gem = BoardManager.GetInstance().Grid.Grid[r, c];
                Image img = gemImages[r, c];
                int idx = (int)gem;

                // Reset position, scale, and alpha
                var canvasGroup = img.GetComponent<CanvasGroup>();
                if (canvasGroup != null) canvasGroup.alpha = 1f;

                gemRects[r, c].anchoredPosition = new Vector2(
                    c * (cellSize + spacing) - totalW / 2f + cellSize / 2f,
                    -(r * (cellSize + spacing) - totalH / 2f + cellSize / 2f)
                );
                gemRects[r, c].localScale = Vector3.one;

                if (gemSprites != null && idx >= 0 && idx < gemSprites.Length && gemSprites[idx] != null)
                {
                    img.sprite = gemSprites[idx];
                    img.color = Color.white;
                }
                else
                {
                    img.sprite = null;
                    img.color = idx == 6 ? charryColor : Color.gray;
                }
            }
        }
    }

    private void OnEnable()
    {
        BoardManager.OnBoardInitialized += OnBoardInit;
        BoardManager.OnMatchesFound += OnMatchesFound;
        BoardManager.OnCascadeComplete += OnCascadeComplete;
    }

    private void OnDisable()
    {
        BoardManager.OnBoardInitialized -= OnBoardInit;
        BoardManager.OnMatchesFound -= OnMatchesFound;
        BoardManager.OnCascadeComplete -= OnCascadeComplete;
    }
}
