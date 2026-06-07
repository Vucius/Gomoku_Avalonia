namespace Gomoku_Avalonia.Models;

public sealed class MoveLogEntry
{
    public MoveLogEntry(int step, int player, BoardPoint point, bool isAiMove, double? confidence = null)
    {
        Step = step;
        Player = player;
        Point = point;
        IsAiMove = isAiMove;
        Confidence = confidence;
    }

    public int Step { get; }

    public int Player { get; }

    public BoardPoint Point { get; }

    public bool IsAiMove { get; }

    public double? Confidence { get; }

    public string PlayerName => IsAiMove ? "AI" : "You";

    public string StoneName => Player == 1 ? "Black" : "White";

    public string Summary
    {
        get
        {
            var confidenceText = Confidence is null ? string.Empty : $" / {Confidence:P0}";
            return $"{Step:00}. {PlayerName} {StoneName} {Point.Coordinate}{confidenceText}";
        }
    }
}
