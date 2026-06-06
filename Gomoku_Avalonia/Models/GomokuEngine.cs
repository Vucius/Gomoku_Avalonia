using System;
using System.Collections.Generic;
using System.Linq;

namespace Gomoku_Avalonia.Models;

public sealed class GomokuEngine
{
    private readonly List<(BoardPoint point, int player)> _history = [];

    public const int BoardSize = 15;

    public int[,] Board { get; } = new int[BoardSize, BoardSize];

    public IReadOnlyList<(BoardPoint point, int player)> History => _history;

    public int StepCount => _history.Count;

    public BoardPoint? LastMove => _history.Count == 0 ? null : _history[^1].point;

    public bool MakeMove(int row, int col, int player)
    {
        if (!IsInside(row, col) || Board[row, col] != 0 || (player != 1 && player != -1))
        {
            return false;
        }

        Board[row, col] = player;
        _history.Add((new BoardPoint(row, col), player));
        return true;
    }

    public bool Undo()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        var last = _history[^1];
        Board[last.point.Row, last.point.Col] = 0;
        _history.RemoveAt(_history.Count - 1);
        return true;
    }

    public void Reset()
    {
        Array.Clear(Board, 0, Board.Length);
        _history.Clear();
    }

    public bool IsFull()
    {
        return _history.Count >= BoardSize * BoardSize;
    }

    public int[][] ToJaggedBoard()
    {
        var rows = new int[BoardSize][];
        for (var row = 0; row < BoardSize; row++)
        {
            rows[row] = new int[BoardSize];
            for (var col = 0; col < BoardSize; col++)
            {
                rows[row][col] = Board[row, col];
            }
        }

        return rows;
    }

    public IReadOnlyList<BoardPoint> CheckWinner(int row, int col)
    {
        if (!IsInside(row, col))
        {
            return [];
        }

        var player = Board[row, col];
        if (player == 0)
        {
            return [];
        }

        var directions = new (int dRow, int dCol)[]
        {
            (1, 0),
            (0, 1),
            (1, 1),
            (1, -1)
        };

        foreach (var (dRow, dCol) in directions)
        {
            var line = new List<BoardPoint> { new(row, col) };
            line.AddRange(Collect(row, col, dRow, dCol, player));
            line.AddRange(Collect(row, col, -dRow, -dCol, player));

            if (line.Count >= 5)
            {
                return line
                    .OrderBy(point => point.Row)
                    .ThenBy(point => point.Col)
                    .Take(5)
                    .ToArray();
            }
        }

        return [];
    }

    private static bool IsInside(int row, int col)
    {
        return row >= 0 && row < BoardSize && col >= 0 && col < BoardSize;
    }

    private IEnumerable<BoardPoint> Collect(int row, int col, int dRow, int dCol, int player)
    {
        for (var i = 1; i < BoardSize; i++)
        {
            var nextRow = row + dRow * i;
            var nextCol = col + dCol * i;
            if (!IsInside(nextRow, nextCol) || Board[nextRow, nextCol] != player)
            {
                yield break;
            }

            yield return new BoardPoint(nextRow, nextCol);
        }
    }
}
