using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gomoku_Avalonia.Models;
using Gomoku_Avalonia.Services;

namespace Gomoku_Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly GomokuEngine _engine = new();
    private readonly GomokuApiClient _apiClient;
    private readonly SoundService _soundService;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _stateFilePath;

    [ObservableProperty]
    private int _moveVersion;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAiFirst;

    [ObservableProperty]
    private bool _isGameOver;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isNetworkWaitVisible;

    [ObservableProperty]
    private BoardSkin _boardSkin = BoardSkin.Wood;

    [ObservableProperty]
    private string _apiBaseUrl = GomokuApiClient.DefaultBaseUrl;

    [ObservableProperty]
    private string _selectedModel = "best_model";

    [ObservableProperty]
    private string _statusText = "Choose a point to start.";

    [ObservableProperty]
    private string _hintText = "No hint yet.";

    [ObservableProperty]
    private string _networkStatusText = "Network: not checked";

    [ObservableProperty]
    private string _networkWaitText = string.Empty;

    [ObservableProperty]
    private BoardPoint? _lastMove;

    [ObservableProperty]
    private BoardPoint? _hintMove;

    [ObservableProperty]
    private IReadOnlyList<BoardPoint> _winningCells = [];

    [ObservableProperty]
    private int _playerWins;

    [ObservableProperty]
    private int _aiWins;

    [ObservableProperty]
    private int _draws;

    public MainViewModel()
        : this(new GomokuApiClient(), new SoundService())
    {
    }

    public MainViewModel(GomokuApiClient apiClient, SoundService soundService)
    {
        _apiClient = apiClient;
        _soundService = soundService;
        _stateFilePath = BuildStateFilePath();
        LoadLocalState();
        ResetGameState();
    }

    public int[,] Board => _engine.Board;

    public ObservableCollection<MoveLogEntry> MoveLog { get; } = [];

    public IReadOnlyList<string> ModelOptions { get; } =
    [
        "best_model",
        "Medium-ppo",
        "easy-iter",
        "Third-dqn",
        "12.13best"
    ];

    public string SkinButtonText => BoardSkin == BoardSkin.Wood ? "Cyberpunk" : "Wood";

    public string OpeningText => IsAiFirst ? "AI opens as black." : "You open as black.";

    public string BusyText => IsBusy ? "AI is thinking..." : string.Empty;

    public string ScoreText => $"You {PlayerWins} · AI {AiWins} · Draw {Draws}";

    public string CompactScoreText => $"You {PlayerWins} | AI {AiWins} | Draw {Draws}";

    public string MoveLogText => MoveLog.Count == 0
        ? "No moves yet."
        : string.Join("   ", MoveLog.TakeLast(3).Select(entry => entry.Summary));

    private int HumanPlayer => IsAiFirst ? -1 : 1;

    private int AiPlayer => IsAiFirst ? 1 : -1;

    partial void OnBoardSkinChanged(BoardSkin value)
    {
        OnPropertyChanged(nameof(SkinButtonText));
        SaveLocalState();
    }

    partial void OnIsAiFirstChanged(bool value)
    {
        OnPropertyChanged(nameof(OpeningText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(BusyText));
    }

    partial void OnPlayerWinsChanged(int value)
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(CompactScoreText));
        SaveLocalState();
    }

    partial void OnAiWinsChanged(int value)
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(CompactScoreText));
        SaveLocalState();
    }

    partial void OnDrawsChanged(int value)
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(CompactScoreText));
        SaveLocalState();
    }

    [RelayCommand]
    private async Task NewGameAsync()
    {
        ResetGameState();
        if (IsAiFirst)
        {
            await RequestAiMoveAsync(isOpeningMove: true);
        }
    }

    [RelayCommand]
    private async Task PlaceHumanMoveAsync(BoardPoint point)
    {
        if (IsBusy || IsGameOver || _engine.Board[point.Row, point.Col] != 0)
        {
            return;
        }

        HintMove = null;
        HintText = "No hint yet.";
        if (!_engine.MakeMove(point.Row, point.Col, HumanPlayer))
        {
            return;
        }

        AddMoveLog(point, HumanPlayer, isAiMove: false);
        await _soundService.PlayMoveAsync();
        RefreshBoard();

        if (HandleTerminalState(point, humanJustMoved: true))
        {
            return;
        }

        await RequestAiMoveAsync(isOpeningMove: false);
    }

    [RelayCommand]
    private void Undo()
    {
        if (IsBusy || _engine.StepCount == 0)
        {
            return;
        }

        var undoCount = _engine.History[^1].player == AiPlayer ? 2 : 1;
        for (var i = 0; i < undoCount && _engine.Undo(); i++)
        {
            if (MoveLog.Count > 0)
            {
                MoveLog.RemoveAt(MoveLog.Count - 1);
            }
        }

        OnPropertyChanged(nameof(MoveLogText));
        IsGameOver = false;
        WinningCells = [];
        HintMove = null;
        HintText = "No hint yet.";
        StatusText = "Move undone.";
        RefreshBoard();
    }

    [RelayCommand]
    private async Task RequestHintAsync()
    {
        if (IsBusy || IsGameOver)
        {
            return;
        }

        await RunAiRequestAsync(
            HumanPlayer,
            result =>
            {
                if (!IsLegalMove(result.Row, result.Col))
                {
                    StatusText = "AI hint was outside the playable board.";
                    return;
                }

                HintMove = new BoardPoint(result.Row, result.Col);
                HintText = $"Suggested {HintMove.Value.Coordinate} · confidence {result.Confidence:P0}";
                StatusText = "Hint ready.";
            },
            "Fetching AI hint...");
    }

    [RelayCommand]
    private void ToggleSkin()
    {
        BoardSkin = BoardSkin == BoardSkin.Wood ? BoardSkin.Cyberpunk : BoardSkin.Wood;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void ApplySettings()
    {
        ApiBaseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl) ? GomokuApiClient.DefaultBaseUrl : ApiBaseUrl.Trim().TrimEnd('/');
        SaveLocalState();
        IsSettingsOpen = false;
        StatusText = "Settings saved.";
    }

    private async Task RequestAiMoveAsync(bool isOpeningMove)
    {
        if (IsGameOver)
        {
            return;
        }

        await RunAiRequestAsync(
            AiPlayer,
            async result =>
            {
                var move = SelectAiMove(result, isOpeningMove);
                if (move is null || !_engine.MakeMove(move.Value.Row, move.Value.Col, AiPlayer))
                {
                    StatusText = "AI returned an invalid move.";
                    return;
                }

                AddMoveLog(move.Value, AiPlayer, isAiMove: true, result.Confidence);
                await _soundService.PlayMoveAsync();
                RefreshBoard();
                HandleTerminalState(move.Value, humanJustMoved: false);
            },
            isOpeningMove ? "Checking opening move..." : "AI is thinking...");
    }

    private async Task RunAiRequestAsync(int player, Func<GomokuMoveResult, Task> onSuccess, string busyText)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = busyText;

        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    var connected = await _apiClient.CheckInternetConnectionAsync(ApiBaseUrl, _shutdown.Token);
                    if (!connected)
                    {
                        await WaitForNetworkRetryAsync("Network unavailable. Waiting for connection...");
                        continue;
                    }

                    var elapsed = Stopwatch.StartNew();
                    var result = await _apiClient.FetchMoveAsync(
                        ApiBaseUrl,
                        _engine.ToJaggedBoard(),
                        player,
                        _engine.StepCount,
                        SelectedModel,
                        _shutdown.Token);
                    elapsed.Stop();

                    IsNetworkWaitVisible = false;
                    NetworkWaitText = string.Empty;
                    NetworkStatusText = $"Connected | {elapsed.ElapsedMilliseconds} ms";
                    await onSuccess(result);
                    return;
                }
                catch (HttpRequestException)
                {
                    await WaitForNetworkRetryAsync("Network request failed. Waiting for connection...");
                }
                catch (TaskCanceledException) when (!_shutdown.IsCancellationRequested)
                {
                    await WaitForNetworkRetryAsync("Network timeout. Waiting for connection...");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task RunAiRequestAsync(int player, Action<GomokuMoveResult> onSuccess, string busyText)
    {
        return RunAiRequestAsync(
            player,
            result =>
            {
                onSuccess(result);
                return Task.CompletedTask;
            },
            busyText);
    }

    private BoardPoint? SelectAiMove(GomokuMoveResult result, bool isOpeningMove)
    {
        if (IsLegalMove(result.Row, result.Col))
        {
            return new BoardPoint(result.Row, result.Col);
        }

        if (isOpeningMove && _engine.StepCount == 0 && _engine.Board[7, 7] == 0)
        {
            return new BoardPoint(7, 7);
        }

        return null;
    }

    private bool HandleTerminalState(BoardPoint move, bool humanJustMoved)
    {
        var winner = _engine.CheckWinner(move.Row, move.Col);
        if (winner.Count > 0)
        {
            WinningCells = winner;
            IsGameOver = true;
            if (humanJustMoved)
            {
                PlayerWins++;
                StatusText = "You win.";
            }
            else
            {
                AiWins++;
                StatusText = "AI wins.";
            }

            _ = _soundService.PlayWinAsync();
            return true;
        }

        if (_engine.IsFull())
        {
            Draws++;
            IsGameOver = true;
            StatusText = "Draw game.";
            return true;
        }

        StatusText = humanJustMoved ? "Waiting for AI." : "Your turn.";
        return false;
    }

    private void AddMoveLog(BoardPoint point, int player, bool isAiMove, double? confidence = null)
    {
        MoveLog.Add(new MoveLogEntry(_engine.StepCount, player, point, isAiMove, confidence));
        OnPropertyChanged(nameof(MoveLogText));
    }

    private bool IsLegalMove(int row, int col)
    {
        return row >= 0 &&
            row < GomokuEngine.BoardSize &&
            col >= 0 &&
            col < GomokuEngine.BoardSize &&
            _engine.Board[row, col] == 0;
    }

    private void ResetGameState()
    {
        _engine.Reset();
        MoveLog.Clear();
        OnPropertyChanged(nameof(MoveLogText));
        IsGameOver = false;
        IsNetworkWaitVisible = false;
        NetworkWaitText = string.Empty;
        WinningCells = [];
        LastMove = null;
        HintMove = null;
        HintText = "No hint yet.";
        StatusText = OpeningText;
        RefreshBoard();
    }

    private void RefreshBoard()
    {
        LastMove = _engine.LastMove;
        MoveVersion++;
        OnPropertyChanged(nameof(Board));
    }

    private async Task WaitForNetworkRetryAsync(string message)
    {
        IsNetworkWaitVisible = true;
        NetworkStatusText = "Waiting for network";
        NetworkWaitText = $"{message} Retrying in 2 seconds.";
        StatusText = "Waiting for network connection.";
        await Task.Delay(TimeSpan.FromSeconds(2), _shutdown.Token);
    }

    private void LoadLocalState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            var state = JsonSerializer.Deserialize<LocalState>(File.ReadAllText(_stateFilePath));
            if (state is null)
            {
                return;
            }

            PlayerWins = state.PlayerWins;
            AiWins = state.AiWins;
            Draws = state.Draws;
            ApiBaseUrl = string.IsNullOrWhiteSpace(state.ApiBaseUrl) ? GomokuApiClient.DefaultBaseUrl : state.ApiBaseUrl;
            SelectedModel = string.IsNullOrWhiteSpace(state.SelectedModel) ? "best_model" : state.SelectedModel;
            BoardSkin = state.BoardSkin;
        }
        catch
        {
            ApiBaseUrl = GomokuApiClient.DefaultBaseUrl;
        }
    }

    private void SaveLocalState()
    {
        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new LocalState(PlayerWins, AiWins, Draws, ApiBaseUrl, SelectedModel, BoardSkin);
            File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Local persistence is best-effort for browser and restricted mobile storage.
        }
    }

    private static string BuildStateFilePath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, "Gomoku_Avalonia", "state.json");
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _apiClient.Dispose();
    }

    private sealed record LocalState(
        int PlayerWins,
        int AiWins,
        int Draws,
        string ApiBaseUrl,
        string SelectedModel,
        BoardSkin BoardSkin);
}
