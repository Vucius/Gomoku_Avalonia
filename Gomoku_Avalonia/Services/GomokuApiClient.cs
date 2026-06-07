using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Gomoku_Avalonia.Services;

public sealed class GomokuApiClient : IDisposable
{
    public const string VercelProxyBaseUrl = "https://vukservices.vercel.app";
    public const string DirectHuggingFaceBaseUrl = "https://mitsutake-model-space.hf.space";
    public const string DefaultBaseUrl = DirectHuggingFaceBaseUrl;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;

    public GomokuApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<GomokuMoveResult> FetchMoveAsync(
        string baseUrl,
        int[][] board,
        int player,
        int step,
        string model,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveEndpoint(baseUrl);
        if (endpoint.IsHuggingFace)
        {
            return await FetchHuggingFaceMoveAsync(endpoint.Uri, board, player, step, model, cancellationToken);
        }

        return await FetchVercelMoveAsync(endpoint.Uri, board, player, step, model, cancellationToken);
    }

    public async Task<GomokuMoveResult> FetchVercelMoveAsync(
        Uri uri,
        int[][] board,
        int player,
        int step,
        string model,
        CancellationToken cancellationToken = default)
    {
        var request = new VercelMoveRequest(board, player, step, model);

        using var response = await _httpClient.PostAsJsonAsync(uri, request, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VercelApiEnvelope>(SerializerOptions, cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("The AI service returned an empty response.");
        }

        if (!payload.Success)
        {
            throw new InvalidOperationException(payload.Message ?? "The AI service rejected the move request.");
        }

        if (payload.Data is null)
        {
            throw new InvalidOperationException("The AI service response does not contain a move.");
        }

        return new GomokuMoveResult(payload.Data.Row, payload.Data.Col, payload.Data.Confidence);
    }

    public async Task<GomokuMoveResult> FetchHuggingFaceMoveAsync(
        Uri uri,
        int[][] board,
        int player,
        int step,
        string model,
        CancellationToken cancellationToken = default)
    {
        var request = new HuggingFaceMoveRequest(FlattenBoard(board), player, step, model);

        using var response = await _httpClient.PostAsJsonAsync(uri, request, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HuggingFaceApiEnvelope>(SerializerOptions, cancellationToken);
        if (payload?.Prediction is null)
        {
            throw new InvalidOperationException("The Hugging Face service response does not contain a prediction.");
        }

        return new GomokuMoveResult(
            payload.Prediction.Row,
            payload.Prediction.Col,
            payload.Prediction.Confidence);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return DefaultBaseUrl;
        }

        return baseUrl.Trim().TrimEnd('/');
    }

    private static ApiEndpoint ResolveEndpoint(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        var uri = new Uri(normalized);

        if (IsHuggingFaceUri(uri))
        {
            return new ApiEndpoint(ResolveHuggingFacePredictUri(uri), true);
        }

        if (uri.AbsolutePath.TrimEnd('/').EndsWith("/api/gomoku/move", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiEndpoint(uri, false);
        }

        return new ApiEndpoint(new Uri($"{normalized}/api/gomoku/move"), false);
    }

    private static bool IsHuggingFaceUri(Uri uri)
    {
        return uri.Host.EndsWith(".hf.space", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.Contains("/gomoku/predict", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ResolveHuggingFacePredictUri(Uri uri)
    {
        if (uri.AbsolutePath.TrimEnd('/').EndsWith("/gomoku/predict", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return new Uri($"{uri.Scheme}://{uri.Authority}/gomoku/predict");
    }

    private static string FlattenBoard(int[][] board)
    {
        return string.Join(",", board.SelectMany(row => row));
    }

    private readonly record struct ApiEndpoint(Uri Uri, bool IsHuggingFace);

    private sealed record VercelMoveRequest(
        [property: JsonPropertyName("board")] int[][] Board,
        [property: JsonPropertyName("player")] int Player,
        [property: JsonPropertyName("step")] int Step,
        [property: JsonPropertyName("model")] string Model);

    private sealed record HuggingFaceMoveRequest(
        [property: JsonPropertyName("board")] string Board,
        [property: JsonPropertyName("player")] int Player,
        [property: JsonPropertyName("step")] int Step,
        [property: JsonPropertyName("model")] string Model);

    private sealed record VercelApiEnvelope(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] MoveData? Data,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record HuggingFaceApiEnvelope(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("prediction")] MoveData? Prediction);

    private sealed record MoveData(
        [property: JsonPropertyName("row")] int Row,
        [property: JsonPropertyName("col")] int Col,
        [property: JsonPropertyName("confidence")] double Confidence);
}

public readonly record struct GomokuMoveResult(int Row, int Col, double Confidence);
