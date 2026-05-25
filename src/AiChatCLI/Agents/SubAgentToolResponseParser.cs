using System.Text.Json;

namespace AiChatCLI;

internal static class SubAgentToolResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SubAgentToolResponse? TryParse(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SubAgentToolResponse>(result, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
