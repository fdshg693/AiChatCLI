using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Provides the AI-callable command execution tool guarded by interactive user approval.
/// </summary>
public partial class CommandTools
{
    /// <summary>
    /// Tool name used in <c>agents.json</c> agent tool lists.
    /// </summary>
    public const string BaseToolName = "command";

    private const int DefaultTimeoutSeconds = 120;
    private const int MaxTimeoutSeconds = 600;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly ICommandApprovalPrompt _approvalPrompt;
    private readonly ICommandExecutor _executor;

    internal CommandTools(ICommandApprovalPrompt approvalPrompt, ICommandExecutor executor)
    {
        _approvalPrompt = approvalPrompt;
        _executor = executor;
    }

    /// <summary>
    /// Runs a local shell command only after the user explicitly approves it in the console.
    /// If the user denies the request, returns a denial result instead of command output.
    /// </summary>
    /// <param name="command">The exact shell command to request for execution.</param>
    /// <param name="timeout_seconds">Maximum execution time in seconds. Defaults to 120 and is capped at 600.</param>
    /// <returns>A JSON string containing ok, status, command, denied, and execution or denial details.</returns>
    [Function]
    public async Task<string> command(string command, int timeout_seconds = DefaultTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Serialize(CommandToolResponse.Invalid("command を指定してください。"));

        var normalizedCommand = command.Trim();
        var approval = await _approvalPrompt.RequestApprovalAsync(normalizedCommand);
        if (!approval.Approved)
            return Serialize(CommandToolResponse.DeniedResult(normalizedCommand, approval.DenialReason));

        try
        {
            var result = await _executor.ExecuteAsync(
                normalizedCommand,
                TimeSpan.FromSeconds(NormalizeTimeoutSeconds(timeout_seconds)));

            return Serialize(CommandToolResponse.Executed(normalizedCommand, result));
        }
        catch (Exception ex)
        {
            return Serialize(CommandToolResponse.ErrorResult(normalizedCommand, ex.Message));
        }
    }

    internal static int NormalizeTimeoutSeconds(int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
            return DefaultTimeoutSeconds;

        return Math.Min(timeoutSeconds, MaxTimeoutSeconds);
    }

    private static string Serialize(CommandToolResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}

internal sealed record CommandToolResponse(
    bool Ok,
    string Status,
    string Command,
    bool Denied,
    int? ExitCode,
    string? Stdout,
    string? Stderr,
    bool? TimedOut,
    string? Reason,
    string? Error)
{
    public static CommandToolResponse Executed(string command, CommandExecutionResult result) =>
        new(
            Ok: result.ExitCode == 0 && !result.TimedOut,
            Status: result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "completed" : "failed",
            Command: command,
            Denied: false,
            ExitCode: result.ExitCode,
            Stdout: result.Stdout,
            Stderr: result.Stderr,
            TimedOut: result.TimedOut,
            Reason: null,
            Error: result.TimedOut ? "コマンド実行がタイムアウトしました。" : null);

    public static CommandToolResponse DeniedResult(string command, string? reason) =>
        new(
            Ok: false,
            Status: "denied",
            Command: command,
            Denied: true,
            ExitCode: null,
            Stdout: null,
            Stderr: null,
            TimedOut: null,
            Reason: reason,
            Error: "ユーザーがコマンド実行を拒否しました。");

    public static CommandToolResponse Invalid(string error) =>
        new(
            Ok: false,
            Status: "invalid_request",
            Command: string.Empty,
            Denied: false,
            ExitCode: null,
            Stdout: null,
            Stderr: null,
            TimedOut: null,
            Reason: null,
            Error: error);

    public static CommandToolResponse ErrorResult(string command, string error) =>
        new(
            Ok: false,
            Status: "error",
            Command: command,
            Denied: false,
            ExitCode: null,
            Stdout: null,
            Stderr: null,
            TimedOut: null,
            Reason: null,
            Error: error);
}
