using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace AiChatCLI.Tests;

public sealed class TavilySearchToolsTests
{
    [Fact]
    public async Task Search_ReturnsNormalizedResultsAndSendsExpectedRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "query": "cursor cli",
                  "results": [
                    {
                      "title": "Cursor CLI",
                      "url": "https://example.com/cursor-cli",
                      "content": "Cursor CLI overview",
                      "score": 0.98
                    }
                  ],
                  "response_time": 0.42,
                  "request_id": "req_123"
                }
                """)
        });
        var tools = CreateTools(handler);

        var result = await tools.search("cursor cli", "advanced");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.tavily.com/search", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("tvly-test-key", handler.LastRequest.Headers.Authorization.Parameter);

        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("cursor cli", requestJson.RootElement.GetProperty("query").GetString());
        Assert.Equal("advanced", requestJson.RootElement.GetProperty("search_depth").GetString());

        using var responseJson = JsonDocument.Parse(result);
        Assert.True(responseJson.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("cursor cli", responseJson.RootElement.GetProperty("query").GetString());
        Assert.Equal("advanced", responseJson.RootElement.GetProperty("searchDepth").GetString());
        Assert.Equal(0.42, responseJson.RootElement.GetProperty("responseTime").GetDouble(), 3);
        Assert.Equal("req_123", responseJson.RootElement.GetProperty("requestId").GetString());

        var firstResult = responseJson.RootElement.GetProperty("results")[0];
        Assert.Equal("Cursor CLI", firstResult.GetProperty("title").GetString());
        Assert.Equal("https://example.com/cursor-cli", firstResult.GetProperty("url").GetString());
        Assert.Equal("Cursor CLI overview", firstResult.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Search_ReturnsValidationErrorForInvalidSearchDepth()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var tools = CreateTools(handler);

        var result = await tools.search("cursor cli", "deep");

        Assert.Null(handler.LastRequest);
        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("search_depth", json.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_ReturnsApiErrorMessageWhenTavilyFails()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """
                {
                  "detail": {
                    "error": "Too many requests."
                  }
                }
                """)
        });
        var tools = CreateTools(handler);

        var result = await tools.search("cursor cli", "fast");

        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("cursor cli", json.RootElement.GetProperty("query").GetString());
        Assert.Equal("fast", json.RootElement.GetProperty("searchDepth").GetString());
        Assert.Contains("Too many requests", json.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Search_ExposesExpectedFunctionName()
    {
        var tools = CreateTools(new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        Assert.Equal(TavilySearchTools.BaseToolName, tools.searchFunctionContract.Name);
    }

    private static TavilySearchTools CreateTools(HttpMessageHandler handler) =>
        new(new TavilySearchClient("tvly-test-key", new HttpClient(handler)));

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
