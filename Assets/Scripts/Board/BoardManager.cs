using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    #region Singleton

    private static BoardManager _instance;

    public static BoardManager GetInstance()
    {
        return _instance;
    }

    #endregion

    #region Events

    public static Action OnBoardInitialized;
    public static Action<List<Vector2Int>> OnMatchesFound;
    public static Action OnCascadeComplete;
    public static Action OnSwapDone;

    #endregion

    public GridModel Grid { get; private set; }
    public bool IsProcessing { get; set; }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    public void InitBoard()
    {
        Grid = new GridModel();
        Grid.Init();
        IsProcessing = false;
        OnBoardInitialized?.Invoke();
    }

    public bool TrySwap(Vector2Int from, Vector2Int to)
    {
        if (IsProcessing) return false;
        if (!GridModel.IsValidPosition(from.y, from.x)) return false;
        if (!GridModel.IsValidPosition(to.y, to.x)) return false;
        if (!GridModel.AreAdjacent(from, to)) return false;

        Grid.Swap(from, to);

        List<Vector2Int> matches = Grid.FindMatches();
        if (matches.Count == 0)
        {
            Grid.Swap(from, to);
            return false;
        }

        IsProcessing = true;
        OnSwapDone?.Invoke();
        StartCoroutine(ProcessMatches(matches));

        return true;
    }

    private IEnumerator ProcessMatches(List<Vector2Int> matches)
    {
        while (matches.Count > 0)
        {
            OnMatchesFound?.Invoke(matches);
            yield return new WaitForSeconds(0.5f);

            Grid.RemoveGems(matches);
            Grid.Cascade();
            Grid.Refill();

            OnCascadeComplete?.Invoke();
            yield return new WaitForSeconds(0.5f);

            matches = Grid.FindMatches();
        }

        IsProcessing = false;
    }
}
