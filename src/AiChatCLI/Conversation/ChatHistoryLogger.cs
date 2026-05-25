namespace AiChatCLI;

internal sealed class ChatHistoryLogger : IDisposable
{
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

    public void LogSessionStart(string modelName, string agentName, string? threadId, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [SESSION] start model={modelName} agent={NormalizeAgentName(agentName)} thread={NormalizeThreadId(threadId)}");
    }

    public void LogSessionEnd(string agentName, string? threadId, string reason, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [SESSION] end agent={NormalizeAgentName(agentName)} thread={NormalizeThreadId(threadId)} reason={reason}");
    }

    public void LogUserInput(string rawLine, string agentName, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [USER] agent={NormalizeAgentName(agentName)} {rawLine}");
    }

    public void LogPromptTransformation(string rawInput, string processedText, string agentName, DateTimeOffset? timestamp = null)
    {
        WriteTaggedLines("TRANSFORM_RAW", rawInput, agentName, timestamp);
        if (string.Equals(rawInput, processedText, StringComparison.Ordinal))
        {
            WriteLine($"[{Timestamp(timestamp)}] [TRANSFORM] agent={NormalizeAgentName(agentName)} 変換なし");
            return;
        }

        WriteTaggedLines("TRANSFORM_FINAL", processedText, agentName, timestamp);
    }

    public void LogMessageSentToModel(string processedText, string agentName, DateTimeOffset? timestamp = null)
    {
        WriteTaggedLines("REQUEST", processedText, agentName, timestamp);
    }

    public void LogAiReply(string reply, string agentName, DateTimeOffset? timestamp = null)
    {
        WriteTaggedLines("AI", reply, agentName, timestamp);
    }

    public void LogToolExecutions(IEnumerable<ToolExecutionRecord> toolExecutions, string agentName, DateTimeOffset? timestamp = null)
    {
        foreach (var toolExecution in toolExecutions)
        {
            WriteLine($"[{Timestamp(timestamp)}] [TOOL] agent={NormalizeAgentName(agentName)} name={toolExecution.FunctionName} id={toolExecution.ToolCallId ?? string.Empty}");
            WriteTaggedLines("TOOL_ARGS", toolExecution.FunctionArguments, agentName, timestamp);
            WriteTaggedLines("TOOL_RESULT", toolExecution.Result, agentName, timestamp);
            LogSubAgentInvocation(toolExecution, agentName, timestamp);
        }
    }

    public void LogSlashCommand(string rawLine, string capturedConsoleOutput, string agentName, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [SLASH] agent={NormalizeAgentName(agentName)} {rawLine}");
        if (string.IsNullOrEmpty(capturedConsoleOutput))
            return;

        var normalized = capturedConsoleOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalized.Split('\n', StringSplitOptions.None))
        {
            WriteLine($"[{Timestamp(timestamp)}] [SLASH_OUT] agent={NormalizeAgentName(agentName)} {line}");
        }
    }

    public void LogAgentChanged(string agentName, string reason, string? threadId, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [AGENT] agent={NormalizeAgentName(agentName)} thread={NormalizeThreadId(threadId)} reason={reason}");
    }

    public void LogThreadChanged(string threadId, string agentName, string reason, DateTimeOffset? timestamp = null)
    {
        WriteLine($"[{Timestamp(timestamp)}] [THREAD] agent={NormalizeAgentName(agentName)} thread={NormalizeThreadId(threadId)} reason={reason}");
    }

    private void WriteTaggedLines(string tag, string? text, string agentName, DateTimeOffset? timestamp)
    {
        var normalizedAgentName = NormalizeAgentName(agentName);
        if (string.IsNullOrEmpty(text))
        {
            WriteLine($"[{Timestamp(timestamp)}] [{tag}] agent={normalizedAgentName} ");
            return;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var line in normalized.Split('\n', StringSplitOptions.None))
        {
            WriteLine($"[{Timestamp(timestamp)}] [{tag}] agent={normalizedAgentName} {line}");
        }
    }

    private void LogSubAgentInvocation(ToolExecutionRecord toolExecution, string agentName, DateTimeOffset? timestamp)
    {
        if (!string.Equals(toolExecution.FunctionName, SubAgentTools.FunctionName, StringComparison.Ordinal))
            return;

        var response = SubAgentToolResponseParser.TryParse(toolExecution.Result);
        if (string.IsNullOrWhiteSpace(response?.SubAgentThreadId))
            return;

        WriteLine($"[{Timestamp(timestamp)}] [SUBAGENT] agent={NormalizeAgentName(agentName)} thread={response.SubAgentThreadId} id={toolExecution.ToolCallId ?? string.Empty}");
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

    private static string Timestamp(DateTimeOffset? timestamp = null) =>
        (timestamp ?? DateTimeOffset.UtcNow)
        .ToLocalTime()
        .ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

    private static string NormalizeAgentName(string agentName) =>
        string.IsNullOrWhiteSpace(agentName) ? "default" : agentName.Trim();

    private static string NormalizeThreadId(string? threadId) =>
        string.IsNullOrWhiteSpace(threadId) ? "-" : threadId.Trim();

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
