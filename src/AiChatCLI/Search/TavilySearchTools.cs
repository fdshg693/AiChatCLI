using System.Text.Json;
using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Exposes the AI-callable Tavily web search tool for the console app.
/// </summary>
public partial class TavilySearchTools
{
    /// <summary>
    /// Tool name used in <c>agents.json</c> agent tool lists.
    /// </summary>
    public const string BaseToolName = "search";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> AllowedSearchDepths =
    [
        "advanced",
        "basic",
        "fast",
        "ultra-fast"
    ];

    private readonly TavilySearchClient _client;

    internal TavilySearchTools(TavilySearchClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Searches the web with Tavily and returns ranked results relevant to the query.
    /// Use <c>basic</c> for general lookups, <c>advanced</c> for higher relevance, and <c>fast</c> or <c>ultra-fast</c> when latency matters most.
    /// </summary>
    /// <param name="query">The search query to execute.</param>
    /// <param name="search_depth">The Tavily search depth: <c>advanced</c>, <c>basic</c>, <c>fast</c>, or <c>ultra-fast</c>.</param>
    /// <returns>A JSON string containing ok, query, searchDepth, results, responseTime, requestId, and error fields.</returns>
    [Function]
    public async Task<string> search(string query, string search_depth = "basic")
    {
        if (string.IsNullOrWhiteSpace(query))
            return Serialize(TavilySearchToolResponse.Failure("query を指定してください。"));

        var normalizedSearchDepth = NormalizeSearchDepth(search_depth);
        if (normalizedSearchDepth is null)
        {
            return Serialize(TavilySearchToolResponse.Failure(
                "search_depth は advanced, basic, fast, ultra-fast のいずれかを指定してください。"));
        }

        try
        {
            var response = await _client.SearchAsync(query.Trim(), normalizedSearchDepth);
            return Serialize(TavilySearchToolResponse.Success(
                response.Query,
                normalizedSearchDepth,
                response.Results.Select(result => new TavilySearchToolResult(
                    result.Title,
                    result.Url,
                    result.Content,
                    result.Score)).ToArray(),
                response.ResponseTime,
                response.RequestId));
        }
        catch (TavilySearchException ex)
        {
            return Serialize(TavilySearchToolResponse.Failure(ex.Message, query.Trim(), normalizedSearchDepth));
        }
        catch (Exception ex)
        {
            return Serialize(TavilySearchToolResponse.Failure(
                $"Tavily search request failed: {ex.Message}",
                query.Trim(),
                normalizedSearchDepth));
        }
    }

    private static string? NormalizeSearchDepth(string searchDepth)
    {
        var normalized = string.IsNullOrWhiteSpace(searchDepth) ? "basic" : searchDepth.Trim().ToLowerInvariant();
        return AllowedSearchDepths.Contains(normalized) ? normalized : null;
    }

    private static string Serialize(TavilySearchToolResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}

internal sealed record TavilySearchToolResponse(
    bool Ok,
    string? Query,
    string? SearchDepth,
    IReadOnlyList<TavilySearchToolResult> Results,
    double? ResponseTime,
    string? RequestId,
    string? Error)
{
    public static TavilySearchToolResponse Success(
        string query,
        string searchDepth,
        IReadOnlyList<TavilySearchToolResult> results,
        double? responseTime,
        string? requestId) =>
        new(true, query, searchDepth, results, responseTime, requestId, null);

    public static TavilySearchToolResponse Failure(
        string error,
        string? query = null,
        string? searchDepth = null) =>
        new(false, query, searchDepth, [], null, null, error);
}

internal sealed record TavilySearchToolResult(
    string? Title,
    string? Url,
    string? Content,
    double? Score);
