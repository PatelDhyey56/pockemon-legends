using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridModel
{
    public const int ROWS = 8;
    public const int COLS = 8;

    public GemType[,] Grid { get; private set; }

    public GridModel()
    {
        Grid = new GemType[ROWS, COLS];
    }

    public void Init()
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                Grid[r, c] = GetRandomGem();
                while (CreatesInitialMatch(r, c))
                    Grid[r, c] = GetRandomGem();
            }
        }
    }

    private bool CreatesInitialMatch(int row, int col)
    {
        if (col >= 2 &&
            Grid[row, col] == Grid[row, col - 1] &&
            Grid[row, col] == Grid[row, col - 2])
            return true;

        if (row >= 2 &&
            Grid[row, col] == Grid[row - 1, col] &&
            Grid[row, col] == Grid[row - 2, col])
            return true;

        return false;
    }

    public List<Vector2Int> FindMatches()
    {
        HashSet<Vector2Int> matched = new HashSet<Vector2Int>();

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS - 2; c++)
            {
                GemType g = Grid[r, c];
                if (g != GemType.Charry && g == Grid[r, c + 1] && g == Grid[r, c + 2])
                {
                    matched.Add(new Vector2Int(c, r));
                    matched.Add(new Vector2Int(c + 1, r));
                    matched.Add(new Vector2Int(c + 2, r));
                }
            }
        }

        for (int c = 0; c < COLS; c++)
        {
            for (int r = 0; r < ROWS - 2; r++)
            {
                GemType g = Grid[r, c];
                if (g != GemType.Charry && g == Grid[r + 1, c] && g == Grid[r + 2, c])
                {
                    matched.Add(new Vector2Int(c, r));
                    matched.Add(new Vector2Int(c, r + 1));
                    matched.Add(new Vector2Int(c, r + 2));
                }
            }
        }

        return new List<Vector2Int>(matched);
    }

    public void RemoveGems(List<Vector2Int> positions)
    {
        foreach (Vector2Int pos in positions)
        {
            Grid[pos.y, pos.x] = GemType.Charry;
        }
    }

    public void Cascade()
    {
        for (int c = 0; c < COLS; c++)
        {
            int writeRow = ROWS - 1;
            for (int r = ROWS - 1; r >= 0; r--)
            {
                if (Grid[r, c] != GemType.Charry)
                {
                    Grid[writeRow, c] = Grid[r, c];
                    if (writeRow != r)
                        Grid[r, c] = GemType.Charry;
                    writeRow--;
                }
            }
        }
    }

    public int Refill()
    {
        int refilled = 0;
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (Grid[r, c] == GemType.Charry)
                {
                    Grid[r, c] = GetRandomGem();
                    refilled++;
                }
            }
        }
        return refilled;
    }

    public static bool IsValidPosition(int row, int col)
    {
        return row >= 0 && row < ROWS && col >= 0 && col < COLS;
    }

    public static bool AreAdjacent(Vector2Int a, Vector2Int b)
    {
        int dr = Mathf.Abs(a.y - b.y);
        int dc = Mathf.Abs(a.x - b.x);
        return (dr == 1 && dc == 0) || (dr == 0 && dc == 1);
    }

    public void Swap(Vector2Int a, Vector2Int b)
    {
        GemType temp = Grid[a.y, a.x];
        Grid[a.y, a.x] = Grid[b.y, b.x];
        Grid[b.y, b.x] = temp;
    }

    public bool FindPossibleMove(out Vector2Int from, out Vector2Int to)
    {
        from = Vector2Int.zero;
        to = Vector2Int.zero;

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                Vector2Int curr = new Vector2Int(c, r);

                // Try swap Right
                if (c < COLS - 1)
                {
                    Vector2Int right = new Vector2Int(c + 1, r);
                    Swap(curr, right);
                    List<Vector2Int> matches = FindMatches();
                    Swap(curr, right); // revert

                    if (matches.Count > 0)
                    {
                        from = curr;
                        to = right;
                        return true;
                    }
                }

                // Try swap Down
                if (r < ROWS - 1)
                {
                    Vector2Int down = new Vector2Int(c, r + 1);
                    Swap(curr, down);
                    List<Vector2Int> matches = FindMatches();
                    Swap(curr, down); // revert

                    if (matches.Count > 0)
                    {
                        from = curr;
                        to = down;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private GemType GetRandomGem()
    {
        return (GemType)Random.Range(0, 6);
    }
}
