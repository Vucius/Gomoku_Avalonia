namespace Gomoku_Avalonia.Models;

public readonly record struct BoardPoint(int Row, int Col)
{
    public string Coordinate => $"{(char)('A' + Col)}{Row + 1}";
}
