using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CascadeMove
{
    public Vector2Int from;
    public Vector2Int to;
    public CascadeMove(Vector2Int from, Vector2Int to) { this.from = from; this.to = to; }
}

public class GridModel
{
    public const int ROWS = 8;
    public const int COLS = 7;

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
                // Empty cells never form match-3 lines
                if (g == GemType.Empty) continue;
                if (g == Grid[r, c + 1] && g == Grid[r, c + 2])
                {
                    matched.Add(new Vector2Int(c,     r));
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
                // Empty cells never form match-3 lines
                if (g == GemType.Empty) continue;
                if (g == Grid[r + 1, c] && g == Grid[r + 2, c])
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
            Grid[pos.y, pos.x] = GemType.Empty;
        }
    }

    /// <summary>
    /// Applies gravity: existing stones fall down to fill Empty gaps.
    /// Returns a list of stones that moved, with their old and new positions.
    /// </summary>
    public List<CascadeMove> Cascade()
    {
        List<CascadeMove> moves = new List<CascadeMove>();
        for (int c = 0; c < COLS; c++)
        {
            int writeRow = ROWS - 1;
            for (int r = ROWS - 1; r >= 0; r--)
            {
                if (Grid[r, c] != GemType.Empty)
                {
                    if (writeRow != r)
                    {
                        moves.Add(new CascadeMove(new Vector2Int(c, r), new Vector2Int(c, writeRow)));
                        Grid[writeRow, c] = Grid[r, c];
                        Grid[r, c] = GemType.Empty;
                    }
                    writeRow--;
                }
            }

            // Ensure any remaining cells above the compacted column are cleared.
            for (int r = writeRow; r >= 0; r--)
            {
                Grid[r, c] = GemType.Empty;
            }
        }
        return moves;
    }

    /// <summary>
    /// Fills every Empty (empty) cell with a random gem.
    /// Returns the positions of all cells that were refilled so
    /// the view layer can play a distinct "drop from top" animation
    /// for brand-new stones vs. existing stones that simply fell down.
    /// </summary>
    public List<Vector2Int> Refill()
    {
        List<Vector2Int> refilled = new List<Vector2Int>();
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (Grid[r, c] == GemType.Empty)
                {
                    Grid[r, c] = GetRandomGem();
                    refilled.Add(new Vector2Int(c, r));
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

    /// <summary>
    /// Returns a random gem type. Charry stones appear at ~8% probability.
    /// Empty is NEVER returned — it is only set internally for empty cells.
    /// </summary>
    private GemType GetRandomGem()
    {
        // Weighted pool: 6 normal types + 1 Charry slot → 7 slots, Charry = ~14%
        // Use 12 slots so Charry is ~8%: 11 normal, 1 Charry
        int roll = Random.Range(0, 12);
        if (roll == 0) return GemType.Charry;
        // Distribute remaining 11 rolls across 6 normal types
        return (GemType)(roll % 6);
    }
}
