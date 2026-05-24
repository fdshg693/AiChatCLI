using System.Text.Json;

namespace AiChatCLI;

internal sealed class ChatHistoryLogger : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StreamWriter? _writer;
    private readonly object _lock = new();

    public ChatHistoryLogger(bool enabled, string directoryPath)
    {
        if (!enabled) return;

        Directory.CreateDirectory(directoryPath);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(directoryPath, $"chat_{stamp}.txt");
        try
        {
            _writer = CreateLogWriter(path);
        }
        catch (IOException)
        {
            path = Path.Combine(directoryPath, $"chat_{stamp}_{Guid.NewGuid():N}.txt");
            _writer = CreateLogWriter(path);
        }

        LogFilePath = path;
    }

    /// <summary>
    /// Absolute path of the log file when logging is enabled; otherwise null.
    /// </summary>
    public string? LogFilePath { get; }

    public bool IsEnabled => _writer is not null;

    public void LogSessionStart(string modelName, string agentName)
    {
        WriteLine($"[{Timestamp()}] [SESSION] start model={modelName} agent={NormalizeAgentName(agentName)}");
    }

    public void LogSessionEnd()
    {
        WriteLine($"[{Timestamp()}] [SESSION] end");
    }

    public void LogUserInput(string rawLine, string agentName)
    {
        WriteLine($"[{Timestamp()}] [USER] agent={NormalizeAgentName(agentName)} {rawLine}");
    }

    public void LogPromptTransformation(string rawInput, string processedText, string agentName)
    {
        WriteTaggedLines("TRANSFORM_RAW", rawInput, agentName);
        if (string.Equals(rawInput, processedText, StringComparison.Ordinal))
        {
            WriteLine($"[{Timestamp()}] [TRANSFORM] agent={NormalizeAgentName(agentName)} 変換なし");
            return;
        }

        WriteTaggedLines("TRANSFORM_FINAL", processedText, agentName);
    }

    public void LogMessageSentToModel(string processedText, string agentName)
    {
        WriteTaggedLines("REQUEST", processedText, agentName);
    }

    public void LogAiReply(string reply, string agentName)
    {
        WriteTaggedLines("AI", reply, agentName);
    }

    public void LogToolExecutions(IEnumerable<ToolExecutionRecord> toolExecutions, string agentName)
    {
        foreach (var toolExecution in toolExecutions)
        {
            WriteLine($"[{Timestamp()}] [TOOL] agent={NormalizeAgentName(agentName)} name={toolExecution.FunctionName} id={toolExecution.ToolCallId ?? string.Empty}");
            WriteTaggedLines("TOOL_ARGS", toolExecution.FunctionArguments, agentName);
            WriteTaggedLines("TOOL_RESULT", toolExecution.Result, agentName);
            LogSubAgentInvocation(toolExecution, agentName);
        }
    }

    public void LogSlashCommand(string rawLine, string capturedConsoleOutput, string agentName)
    {
        WriteLine($"[{Timestamp()}] [SLASH] agent={NormalizeAgentName(agentName)} {rawLine}");
        if (string.IsNullOrEmpty(capturedConsoleOutput))
            return;

        var normalized = capturedConsoleOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalized.Split('\n', StringSplitOptions.None))
        {
            WriteLine($"[{Timestamp()}] [SLASH_OUT] agent={NormalizeAgentName(agentName)} {line}");
        }
    }

    private void WriteTaggedLines(string tag, string? text, string agentName)
    {
        var normalizedAgentName = NormalizeAgentName(agentName);
        if (string.IsNullOrEmpty(text))
        {
            WriteLine($"[{Timestamp()}] [{tag}] agent={normalizedAgentName} ");
            return;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var line in normalized.Split('\n', StringSplitOptions.None))
        {
            WriteLine($"[{Timestamp()}] [{tag}] agent={normalizedAgentName} {line}");
        }
    }

    private void LogSubAgentInvocation(ToolExecutionRecord toolExecution, string agentName)
    {
        if (!string.Equals(toolExecution.FunctionName, SubAgentTools.FunctionName, StringComparison.Ordinal))
            return;

        var response = TryReadSubAgentResponse(toolExecution.Result);
        if (string.IsNullOrWhiteSpace(response?.SubAgentThreadId))
            return;

        WriteLine($"[{Timestamp()}] [SUBAGENT] agent={NormalizeAgentName(agentName)} thread={response.SubAgentThreadId} id={toolExecution.ToolCallId ?? string.Empty}");
    }

    private static SubAgentToolResponse? TryReadSubAgentResponse(string? result)
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

    private static StreamWriter CreateLogWriter(string path) =>
        new(
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            TextEncodingDefaults.Utf8NoBom)
        {
            AutoFlush = true
        };

    private void WriteLine(string line)
    {
        if (_writer is null) return;
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

    private static string NormalizeAgentName(string agentName) =>
        string.IsNullOrWhiteSpace(agentName) ? "default" : agentName.Trim();

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
