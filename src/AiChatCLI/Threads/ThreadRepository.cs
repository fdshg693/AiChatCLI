using System.Globalization;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace AiChatCLI;

public sealed class ThreadRepository
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ThreadRepository(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath を指定してください。", nameof(directoryPath));

        DirectoryPath = Path.GetFullPath(directoryPath.Trim());
        Directory.CreateDirectory(DirectoryPath);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public string DirectoryPath { get; }

    public static ThreadRepository CreateSubAgentRepository(string directoryPath) =>
        new(directoryPath);

    public string CreateThread(
        string modelName,
        string agentName,
        string systemPrompt,
        string threadIdPrefix = "thread")
    {
        string threadId;
        string path;

        do
        {
            threadId = CreateThreadId(threadIdPrefix);
            path = GetThreadFilePath(threadId);
        } while (File.Exists(path));

        WriteEvent(path, ThreadEvent.ThreadCreated(threadId, modelName, agentName, systemPrompt), createNew: true);
        return threadId;
    }

    public void AppendEvent(ThreadEvent threadEvent)
    {
        var path = GetThreadFilePath(threadEvent.ThreadId);
        WriteEvent(path, threadEvent, createNew: false);
    }

    public bool ThreadExists(string threadId)
    {
        try
        {
            return File.Exists(GetThreadFilePath(threadId));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public string GetThreadFilePath(string threadId)
    {
        var normalizedThreadId = NormalizeThreadId(threadId);
        return Path.Combine(DirectoryPath, $"{normalizedThreadId}.jsonl");
    }

    public IReadOnlyList<ThreadEvent> ReadEvents(string threadId) =>
        ReadEventsFromPath(GetThreadFilePath(threadId));

    public IReadOnlyList<ThreadSummary> ListThreads(ThreadProjector projector)
    {
        var summaries = new List<ThreadSummary>();

        foreach (var filePath in Directory.EnumerateFiles(DirectoryPath, "*.jsonl"))
        {
            var events = ReadEventsFromPath(filePath);
            if (events.Count == 0)
                continue;

            var snapshot = projector.Project(filePath, events);
            summaries.Add(projector.CreateSummary(snapshot));
        }

        return summaries
            .OrderByDescending(summary => summary.LastUpdatedAt)
            .ToArray();
    }

    private IReadOnlyList<ThreadEvent> ReadEventsFromPath(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("スレッドログが見つかりません。", filePath);

        var events = new List<ThreadEvent>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var threadEvent = JsonSerializer.Deserialize<ThreadEvent>(line, _jsonOptions)
                              ?? throw new InvalidDataException($"空のイベントを読み込みました: {filePath}:{lineNumber}");

            events.Add(threadEvent);
        }

        return events;
    }

    private void WriteEvent(string filePath, ThreadEvent threadEvent, bool createNew)
    {
        var fileMode = createNew ? FileMode.CreateNew : FileMode.Append;
        var serialized = JsonSerializer.Serialize(threadEvent, _jsonOptions);

        using var stream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(serialized);
    }

    public static string CreateThreadId(string prefix = "thread")
    {
        var effectivePrefix = NormalizeThreadId(prefix);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{effectivePrefix}_{stamp}_{suffix}";
    }

    private static string NormalizeThreadId(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("threadId を指定してください。", nameof(threadId));

        var normalized = threadId.Trim();

        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("不正な threadId です。", nameof(threadId));
        }

        return normalized;
    }
}
