using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace AiChatCLI;

public sealed class TavilySearchClient
{
    private const string SearchEndpoint = "https://api.tavily.com/search";

    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public TavilySearchClient(string apiKey, HttpClient httpClient)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey)
            ? throw new ArgumentException("apiKey を指定してください。", nameof(apiKey))
            : apiKey.Trim();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<TavilySearchResponse> SearchAsync(string query, string searchDepth, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchDepth);

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query,
                    search_depth = searchDepth
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new TavilySearchException(response.StatusCode, BuildErrorMessage(response.StatusCode, body));

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var results = new List<TavilySearchResult>();

        if (TryGetProperty(root, "results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var resultElement in resultsElement.EnumerateArray())
            {
                results.Add(new TavilySearchResult(
                    ReadString(resultElement, "title"),
                    ReadString(resultElement, "url"),
                    ReadString(resultElement, "content"),
                    ReadDouble(resultElement, "score")));
            }
        }

        return new TavilySearchResponse(
            ReadString(root, "query") ?? query,
            results,
            ReadDouble(root, "response_time"),
            ReadString(root, "request_id"));
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string body)
    {
        string? apiError = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (TryGetProperty(root, "detail", out var detailElement) &&
                    detailElement.ValueKind == JsonValueKind.Object)
                {
                    apiError = ReadString(detailElement, "error");
                }
            }
            catch (JsonException)
            {
            }
        }

        var statusText = $"{(int)statusCode} {statusCode}";
        return string.IsNullOrWhiteSpace(apiError)
            ? $"Tavily search request failed ({statusText})."
            : $"Tavily search request failed ({statusText}): {apiError}";
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

public sealed record TavilySearchResponse(
    string Query,
    IReadOnlyList<TavilySearchResult> Results,
    double? ResponseTime,
    string? RequestId);

public sealed record TavilySearchResult(
    string? Title,
    string? Url,
    string? Content,
    double? Score);

public sealed class TavilySearchException : Exception
{
    public TavilySearchException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
