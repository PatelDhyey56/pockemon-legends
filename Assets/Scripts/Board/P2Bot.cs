using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class P2Bot : MonoBehaviour
{
    private BoardInputHandler _inputHandler;
    private BoardManager _board;
    private bool _isBotMoving;

    private static readonly WaitForSeconds _initialDelay = new WaitForSeconds(0.8f);
    private static readonly WaitForSeconds _postMoveDelay = new WaitForSeconds(0.3f);

    private void Start()
    {
        _inputHandler = FindFirstObjectByType<BoardInputHandler>();
        if (_inputHandler == null)
            Debug.LogError("[P2Bot] BoardInputHandler not found!");
        StartCoroutine(BotLoop());
    }

    private void OnEnable()
    {
        BoardManager.OnBoardInitialized += OnBoardInit;
        BoardManager.OnTurnChanged += OnTurnChanged;
        BoardManager.OnGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        BoardManager.OnBoardInitialized -= OnBoardInit;
        BoardManager.OnTurnChanged -= OnTurnChanged;
        BoardManager.OnGameOver -= OnGameOver;
    }

    private void OnBoardInit()
    {
        _isBotMoving = false;
        StopAllCoroutines();
        StartCoroutine(BotLoop());
    }

    private void OnTurnChanged() { }

    private void OnGameOver(int losingPlayerIdx)
    {
        _isBotMoving = false;
        StopAllCoroutines();
    }

    private IEnumerator BotLoop()
    {
        yield return _initialDelay;

        while (true)
        {
            yield return null;

            _board = BoardManager.GetInstance();
            if (_board == null || _inputHandler == null) continue;
            if (_board.Players == null || _board.Grid == null) continue;
            if (_board.IsProcessing || _board.IsWaitingForEvolutionSelection) continue;
            if (_isBotMoving) continue;
            if (_board.ActivePlayerIndex != 1) continue;
            if (_board.Players[1].MovesRemaining <= 0) continue;

            _isBotMoving = true;
            float thinkingTime = Random.Range(0.5f, 7.0f);
            yield return new WaitForSeconds(thinkingTime);

            // Re-check conditions after delay
            if (_board == null || _board.Grid == null) { _isBotMoving = false; continue; }
            if (_board.IsProcessing || _board.IsWaitingForEvolutionSelection) { _isBotMoving = false; continue; }
            if (_board.ActivePlayerIndex != 1 || _board.Players[1].MovesRemaining <= 0) { _isBotMoving = false; continue; }

            MakeMove();

            yield return _postMoveDelay;
            _isBotMoving = false;
        }
    }

    private struct BotMove
    {
        public Vector2Int From;
        public Vector2Int To;
        public int Score;
        public bool IsStrategic; // Matches bot's creature type or evolution
    }

    private void MakeMove()
    {
        if (_board == null || _board.Grid == null) return;
        if (_board.IsProcessing) return;

        List<GemType> botCreatureTypes = new List<GemType>();
        if (_board.Players != null && _board.Players.Length > 1 && _board.Players[1].Creatures != null)
        {
            foreach (var p in _board.Players[1].Creatures)
            {
                botCreatureTypes.Add(p.Type);
            }
        }

        List<BotMove> allMoves = EvaluateAllMoves(botCreatureTypes);

        if (allMoves.Count > 0)
        {
            // Separate strategic moves and normal moves
            List<BotMove> strategicMoves = new List<BotMove>();
            List<BotMove> normalMoves = new List<BotMove>();

            foreach (var move in allMoves)
            {
                if (move.IsStrategic)
                    strategicMoves.Add(move);
                else
                    normalMoves.Add(move);
            }

            BotMove chosenMove;
            if (strategicMoves.Count > 0)
            {
                // Find highest score in strategic moves
                int maxScore = int.MinValue;
                foreach (var m in strategicMoves)
                {
                    if (m.Score > maxScore) maxScore = m.Score;
                }

                // Gather all strategic moves that have the highest score
                List<BotMove> bestStrategicMoves = new List<BotMove>();
                foreach (var m in strategicMoves)
                {
                    if (m.Score == maxScore)
                        bestStrategicMoves.Add(m);
                }

                // Choose one of the best strategic moves randomly
                chosenMove = bestStrategicMoves[Random.Range(0, bestStrategicMoves.Count)];
            }
            else
            {
                // Find highest score in normal moves
                int maxScore = int.MinValue;
                foreach (var m in normalMoves)
                {
                    if (m.Score > maxScore) maxScore = m.Score;
                }

                List<BotMove> bestNormalMoves = new List<BotMove>();
                foreach (var m in normalMoves)
                {
                    if (m.Score == maxScore)
                        bestNormalMoves.Add(m);
                }

                chosenMove = bestNormalMoves[Random.Range(0, bestNormalMoves.Count)];
            }

            _inputHandler.ExecuteBotSwap(chosenMove.From, chosenMove.To);
            return;
        }

        // Fallback if no match-forming swaps exist on the board
        Vector2Int from, to;
        if (TryRandomSwap(out from, out to))
            _inputHandler.ExecuteBotSwap(from, to);
    }

    private List<BotMove> EvaluateAllMoves(List<GemType> botCreatureTypes)
    {
        List<BotMove> moves = new List<BotMove>();
        if (_board == null || _board.Grid == null) return moves;

        GridModel grid = _board.Grid;

        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                Vector2Int curr = new Vector2Int(c, r);

                // Try Right swap
                if (c < GridModel.COLS - 1)
                {
                    Vector2Int right = new Vector2Int(c + 1, r);
                    EvaluateSwap(grid, curr, right, botCreatureTypes, moves);
                }

                // Try Down swap
                if (r < GridModel.ROWS - 1)
                {
                    Vector2Int down = new Vector2Int(c, r + 1);
                    EvaluateSwap(grid, curr, down, botCreatureTypes, moves);
                }
            }
        }

        return moves;
    }

    private void EvaluateSwap(GridModel grid, Vector2Int a, Vector2Int b, List<GemType> botCreatureTypes, List<BotMove> moves)
    {
        grid.Swap(a, b);
        List<Vector2Int> matches = grid.FindMatches();
        
        if (matches.Count > 0)
        {
            int score = CalculateMoveScore(grid, matches, botCreatureTypes, out bool isStrategic);
            moves.Add(new BotMove
            {
                From = a,
                To = b,
                Score = score,
                IsStrategic = isStrategic
            });
        }
        grid.Swap(a, b); // Revert
    }

    private int CalculateMoveScore(GridModel grid, List<Vector2Int> matches, List<GemType> botCreatureTypes, out bool isStrategic)
    {
        isStrategic = false;
        int score = 0;

        // 1. Score based on match size
        if (matches.Count == 3)
        {
            score += 10;
        }
        else if (matches.Count == 4)
        {
            score += 50; // extra move is very valuable
        }
        else if (matches.Count >= 5)
        {
            score += 100;
        }

        // 2. Score based on matched gem types
        foreach (Vector2Int pos in matches)
        {
            GemType type = grid.Grid[pos.y, pos.x];
            if (type == GemType.Charry)
            {
                score += 20;
                isStrategic = true;
            }
            else if (botCreatureTypes.Contains(type))
            {
                score += 15;
                isStrategic = true;
            }
            else
            {
                score += 2;
            }
        }

        return score;
    }

    private bool TryRandomSwap(out Vector2Int from, out Vector2Int to)
    {
        from = Vector2Int.zero;
        to = Vector2Int.zero;

        if (_board?.Grid == null) return false;

        var possible = new List<(Vector2Int, Vector2Int)>();
        for (int r = 0; r < GridModel.ROWS; r++)
        {
            for (int c = 0; c < GridModel.COLS; c++)
            {
                Vector2Int curr = new Vector2Int(c, r);
                if (c < GridModel.COLS - 1)
                    possible.Add((curr, new Vector2Int(c + 1, r)));
                if (r < GridModel.ROWS - 1)
                    possible.Add((curr, new Vector2Int(c, r + 1)));
            }
        }

        if (possible.Count == 0) return false;
        var pick = possible[Random.Range(0, possible.Count)];
        from = pick.Item1;
        to = pick.Item2;
        return true;
    }
}
