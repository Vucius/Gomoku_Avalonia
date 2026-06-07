using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Gomoku_Avalonia.Models;

namespace Gomoku_Avalonia.Views;

public sealed class GomokuBoardView : Control
{
    public static readonly StyledProperty<int[,]> BoardProperty =
        AvaloniaProperty.Register<GomokuBoardView, int[,]>(nameof(Board));

    public static readonly StyledProperty<int> MoveVersionProperty =
        AvaloniaProperty.Register<GomokuBoardView, int>(nameof(MoveVersion));

    public static readonly StyledProperty<BoardPoint?> LastMoveProperty =
        AvaloniaProperty.Register<GomokuBoardView, BoardPoint?>(nameof(LastMove));

    public static readonly StyledProperty<BoardPoint?> HintMoveProperty =
        AvaloniaProperty.Register<GomokuBoardView, BoardPoint?>(nameof(HintMove));

    public static readonly StyledProperty<IReadOnlyList<BoardPoint>?> WinningCellsProperty =
        AvaloniaProperty.Register<GomokuBoardView, IReadOnlyList<BoardPoint>?>(nameof(WinningCells));

    public static readonly StyledProperty<BoardSkin> SkinProperty =
        AvaloniaProperty.Register<GomokuBoardView, BoardSkin>(nameof(Skin));

    public static readonly StyledProperty<ICommand?> CellSelectedCommandProperty =
        AvaloniaProperty.Register<GomokuBoardView, ICommand?>(nameof(CellSelectedCommand));

    private readonly DispatcherTimer _pulseTimer;

    static GomokuBoardView()
    {
        AffectsRender<GomokuBoardView>(
            BoardProperty,
            MoveVersionProperty,
            LastMoveProperty,
            HintMoveProperty,
            WinningCellsProperty,
            SkinProperty);
    }

    public GomokuBoardView()
    {
        ClipToBounds = true;
        Focusable = true;
        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        _pulseTimer.Tick += (_, _) => InvalidateVisual();
    }

    public int[,] Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public int MoveVersion
    {
        get => GetValue(MoveVersionProperty);
        set => SetValue(MoveVersionProperty, value);
    }

    public BoardPoint? LastMove
    {
        get => GetValue(LastMoveProperty);
        set => SetValue(LastMoveProperty, value);
    }

    public BoardPoint? HintMove
    {
        get => GetValue(HintMoveProperty);
        set => SetValue(HintMoveProperty, value);
    }

    public IReadOnlyList<BoardPoint>? WinningCells
    {
        get => GetValue(WinningCellsProperty);
        set => SetValue(WinningCellsProperty, value);
    }

    public BoardSkin Skin
    {
        get => GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    public ICommand? CellSelectedCommand
    {
        get => GetValue(CellSelectedCommandProperty);
        set => SetValue(CellSelectedCommandProperty, value);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _pulseTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _pulseTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var metrics = GetMetrics();
        if (metrics is null)
        {
            return;
        }

        DrawBoardSurface(context, metrics.Value);
        DrawGrid(context, metrics.Value);
        DrawHighlights(context, metrics.Value);
        DrawStones(context, metrics.Value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var command = CellSelectedCommand;
        var hit = HitTestCell(e.GetPosition(this));
        if (hit is null || command is null || !command.CanExecute(hit.Value))
        {
            return;
        }

        command.Execute(hit.Value);
        e.Handled = true;
    }

    private void DrawBoardSurface(DrawingContext context, BoardMetrics metrics)
    {
        if (Skin == BoardSkin.Cyberpunk)
        {
            context.DrawRectangle(Brush("#090f1d"), null, metrics.BoardRect, 10, 10);
            for (var i = 0; i < 42; i++)
            {
                var x = metrics.BoardRect.X + (i * 67 % Math.Max(1, (int)metrics.BoardRect.Width));
                var y = metrics.BoardRect.Y + (i * 43 % Math.Max(1, (int)metrics.BoardRect.Height));
                context.DrawEllipse(Brush("#554ae6ff"), null, new Point(x, y), 1.2, 1.2);
            }

            context.DrawRectangle(Brush("#101827"), new Pen(Brush("#992ff3ff"), 2), metrics.BoardRect, 8, 8);
            return;
        }

        var shadowRect = metrics.BoardRect.Translate(new Vector(0, 3));
        context.DrawRectangle(Brush("#240f1720"), null, shadowRect, 12, 12);
        context.DrawRectangle(Brush("#e5bc78"), new Pen(Brush("#8a6b4828"), 1.2), metrics.BoardRect, 10, 10);
        context.DrawRectangle(Brush("#22fff8ea"), null, metrics.BoardRect.Deflate(5), 7, 7);

        for (var i = 0; i < 22; i++)
        {
            var y = metrics.BoardRect.Y + 10 + (metrics.BoardRect.Height - 20) * i / 21.0;
            var start = new Point(metrics.BoardRect.X + 12, y);
            var end = new Point(metrics.BoardRect.Right - 12, y + Math.Sin(i * 0.8) * 3);
            context.DrawLine(new Pen(Brush("#48613a19"), 0.8), start, end);
        }
    }

    private void DrawGrid(DrawingContext context, BoardMetrics metrics)
    {
        var gridPen = Skin == BoardSkin.Cyberpunk
            ? new Pen(Brush("#4edfff"), 1.2)
            : new Pen(Brush("#aa4a2f19"), 0.95);

        for (var i = 0; i < GomokuEngine.BoardSize; i++)
        {
            var x = metrics.Origin.X + metrics.Gap * i;
            var y = metrics.Origin.Y + metrics.Gap * i;
            context.DrawLine(gridPen, new Point(x, metrics.Origin.Y), new Point(x, metrics.GridEnd.Y));
            context.DrawLine(gridPen, new Point(metrics.Origin.X, y), new Point(metrics.GridEnd.X, y));
        }

        var starBrush = Skin == BoardSkin.Cyberpunk ? Brush("#e9faff") : Brush("#9a3c2413");
        foreach (var row in new[] { 3, 7, 11 })
        {
            foreach (var col in new[] { 3, 7, 11 })
            {
                var center = metrics.CellCenter(row, col);
                context.DrawEllipse(starBrush, null, center, 3.3, 3.3);
            }
        }
    }

    private void DrawHighlights(DrawingContext context, BoardMetrics metrics)
    {
        var pulse = 0.55 + 0.45 * Math.Sin(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 155.0);

        if (HintMove is { } hint)
        {
            var center = metrics.CellCenter(hint.Row, hint.Col);
            var radius = metrics.Gap * (0.46 + 0.12 * pulse);
            context.DrawEllipse(null, new Pen(Brush("#3975a85e"), 2.4), center, radius, radius);
        }

        if (LastMove is { } lastMove)
        {
            var center = metrics.CellCenter(lastMove.Row, lastMove.Col);
            var radius = metrics.Gap * (0.24 + 0.08 * pulse);
            var brush = Skin == BoardSkin.Cyberpunk ? Brush("#cc4cff8b") : Brush("#b0663d24");
            context.DrawEllipse(brush, null, center, radius, radius);
        }
    }

    private void DrawStones(DrawingContext context, BoardMetrics metrics)
    {
        var board = Board;
        if (board is null)
        {
            return;
        }

        var winningCells = WinningCells ?? [];
        for (var row = 0; row < GomokuEngine.BoardSize; row++)
        {
            for (var col = 0; col < GomokuEngine.BoardSize; col++)
            {
                var player = board[row, col];
                if (player == 0)
                {
                    continue;
                }

                var point = new BoardPoint(row, col);
                var center = metrics.CellCenter(row, col);
                var radius = metrics.Gap * 0.42;
                var isWinning = winningCells.Contains(point);
                DrawStone(context, center, radius, player, isWinning);
            }
        }
    }

    private void DrawStone(DrawingContext context, Point center, double radius, int player, bool isWinning)
    {
        if (Skin == BoardSkin.Cyberpunk)
        {
            var glow = player == 1 ? "#8831e7ff" : "#88ff3c7d";
            var fill = player == 1 ? "#61efff" : "#ff5f8e";
            context.DrawEllipse(Brush(glow), null, center, radius * 1.22, radius * 1.22);
            context.DrawEllipse(Brush(fill), new Pen(Brush("#ccffffff"), isWinning ? 3 : 1), center, radius, radius);
            return;
        }

        if (player == 1)
        {
            context.DrawEllipse(Brush("#151515"), new Pen(Brush(isWinning ? "#c53c2f" : "#111111"), isWinning ? 2.6 : 0.9), center, radius, radius);
            context.DrawEllipse(Brush("#33ffffff"), null, new Point(center.X - radius * 0.28, center.Y - radius * 0.35), radius * 0.16, radius * 0.16);
            return;
        }

        context.DrawEllipse(Brush("#fbf7ec"), new Pen(Brush(isWinning ? "#c53c2f" : "#9c8f76"), isWinning ? 2.6 : 0.9), center, radius, radius);
        context.DrawEllipse(Brush("#aaffffff"), null, new Point(center.X - radius * 0.24, center.Y - radius * 0.32), radius * 0.18, radius * 0.18);
    }

    private BoardPoint? HitTestCell(Point pointer)
    {
        var metrics = GetMetrics();
        if (metrics is null)
        {
            return null;
        }

        var col = (int)Math.Round((pointer.X - metrics.Value.Origin.X) / metrics.Value.Gap);
        var row = (int)Math.Round((pointer.Y - metrics.Value.Origin.Y) / metrics.Value.Gap);
        if (row < 0 || row >= GomokuEngine.BoardSize || col < 0 || col >= GomokuEngine.BoardSize)
        {
            return null;
        }

        var center = metrics.Value.CellCenter(row, col);
        if (Math.Abs(pointer.X - center.X) > metrics.Value.Gap * 0.48 ||
            Math.Abs(pointer.Y - center.Y) > metrics.Value.Gap * 0.48)
        {
            return null;
        }

        return new BoardPoint(row, col);
    }

    private BoardMetrics? GetMetrics()
    {
        if (Bounds.Width < 80 || Bounds.Height < 80)
        {
            return null;
        }

        var side = Math.Max(72, Math.Min(Bounds.Width, Bounds.Height) - 8);
        var boardRect = new Rect((Bounds.Width - side) / 2, (Bounds.Height - side) / 2, side, side);
        var inset = side * 0.052;
        var gap = (side - inset * 2) / (GomokuEngine.BoardSize - 1);
        var origin = new Point(boardRect.X + inset, boardRect.Y + inset);
        var gridEnd = new Point(origin.X + gap * (GomokuEngine.BoardSize - 1), origin.Y + gap * (GomokuEngine.BoardSize - 1));
        return new BoardMetrics(boardRect, origin, gridEnd, gap);
    }

    private static IBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    private readonly record struct BoardMetrics(Rect BoardRect, Point Origin, Point GridEnd, double Gap)
    {
        public Point CellCenter(int row, int col)
        {
            return new Point(Origin.X + Gap * col, Origin.Y + Gap * row);
        }
    }
}
