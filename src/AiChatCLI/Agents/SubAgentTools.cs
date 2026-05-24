using System.Text.Json;
using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Exposes the AI-callable tool that delegates a task to a fresh sub-agent session.
/// </summary>
public partial class SubAgentTools
{
    /// <summary>
    /// Tool name used in <c>agents.json</c> agent tool lists and tool registration.
    /// </summary>
    public const string FunctionName = "sub_agent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SubAgentRunner _runner;

    internal SubAgentTools(SubAgentRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Starts a fresh sub-agent with no prior conversation context, gives it the prompt, and returns its textual result.
    /// The sub-agent may use normal tools such as memory, but it cannot call this sub-agent tool recursively.
    /// </summary>
    /// <param name="prompt">The complete task prompt for the fresh sub-agent to work on.</param>
    /// <returns>A JSON string containing ok, subAgentThreadId, result, and error fields.</returns>
    [Function]
    public async Task<string> sub_agent(string prompt)
    {
        if (SubAgentExecutionContext.IsActive)
            return Serialize(new SubAgentToolResponse(
                false,
                null,
                null,
                "sub_agent はサブエージェント内では使用できません。"));

        if (string.IsNullOrWhiteSpace(prompt))
            return Serialize(new SubAgentToolResponse(
                false,
                null,
                null,
                "prompt を指定してください。"));

        try
        {
            var result = await _runner.RunAsync(prompt);
            return Serialize(new SubAgentToolResponse(true, result.ThreadId, result.FinalReply, null));
        }
        catch (Exception ex)
        {
            return Serialize(new SubAgentToolResponse(false, null, null, ex.Message));
        }
    }

    private static string Serialize(SubAgentToolResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}
