using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Gomoku_Avalonia.Services;

public sealed class GomokuApiClient : IDisposable
{
    public const string DefaultBaseUrl = "https://vukservices.vercel.app";

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
        var uri = new Uri($"{NormalizeBaseUrl(baseUrl)}/api/gomoku/move");
        var request = new MoveRequest(board, player, step, model);

        using var response = await _httpClient.PostAsJsonAsync(uri, request, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope>(SerializerOptions, cancellationToken);
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

    public async Task<bool> CheckInternetConnectionAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = new HttpRequestMessage(HttpMethod.Get, NormalizeBaseUrl(baseUrl));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
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

    private sealed record MoveRequest(
        [property: JsonPropertyName("board")] int[][] Board,
        [property: JsonPropertyName("player")] int Player,
        [property: JsonPropertyName("step")] int Step,
        [property: JsonPropertyName("model")] string Model);

    private sealed record ApiEnvelope(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] MoveData? Data,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record MoveData(
        [property: JsonPropertyName("row")] int Row,
        [property: JsonPropertyName("col")] int Col,
        [property: JsonPropertyName("confidence")] double Confidence);
}

public readonly record struct GomokuMoveResult(int Row, int Col, double Confidence);
