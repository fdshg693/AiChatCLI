namespace AiChatCLI.Commands;

internal sealed class ThreadCommand : ISlashCommand
{
    private static readonly CommandHelpEntry[] HelpEntryDefinitions =
    [
        new(
            "/thread",
            "thread の一覧表示・参照・切り替え・新規作成",
            [
                "/thread",
                "/thread list",
                "/thread current",
                "/thread use <id>",
                "/thread new"
            ])
    ];

    private readonly ThreadSessionManager _threadSessionManager;
    private readonly IChatTraceRecorder _chatTraceRecorder;

    public ThreadCommand(ThreadSessionManager threadSessionManager, IChatTraceRecorder chatTraceRecorder)
    {
        _threadSessionManager = threadSessionManager;
        _chatTraceRecorder = chatTraceRecorder;
    }

    public string Name => "thread";
    public string Description => "thread を操作";
    public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntryDefinitions;

    public void Execute(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            ShowHelp(output);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                ListThreads(output);
                break;
            case "current":
                ShowCurrentThread(output);
                break;
            case "use":
                UseThread(args, output);
                break;
            case "new":
                CreateThread(output);
                break;
            default:
                output.WriteLine($"不明な thread アクション: {args[0]}");
                ShowHelp(output);
                break;
        }
    }

    private static void ShowHelp(TextWriter output)
    {
        CommandConventions.ShowHelpEntry(HelpEntryDefinitions[0], output);
    }

    private void ListThreads(TextWriter output)
    {
        if (!_threadSessionManager.IsEnabled)
        {
            output.WriteLine("会話履歴ログが無効のため thread 操作は利用できません。");
            return;
        }

        var threads = _threadSessionManager.ListThreads();
        if (threads.Count == 0)
        {
            output.WriteLine("保存済みのスレッドはありません。");
            return;
        }

        output.WriteLine("--- スレッド一覧 ---");
        foreach (var thread in threads)
        {
            var marker = string.Equals(thread.ThreadId, _threadSessionManager.CurrentThreadId, StringComparison.Ordinal)
                ? " [現在]"
                : string.Empty;
            output.WriteLine(
                $"  {thread.ThreadId}{marker} : agent={thread.CurrentAgentName}, messages={thread.MessageCount}, updated={thread.LastUpdatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        }

        output.WriteLine("--------------------");
    }

    private void ShowCurrentThread(TextWriter output)
    {
        if (!_threadSessionManager.IsEnabled)
        {
            output.WriteLine("会話履歴ログが無効のため thread 操作は利用できません。");
            return;
        }

        var thread = _threadSessionManager.GetCurrentThreadSummary();
        if (thread is null)
        {
            output.WriteLine("現在のスレッドはありません。");
            return;
        }

        output.WriteLine("--- 現在のスレッド ---");
        output.WriteLine($"ID: {thread.ThreadId}");
        output.WriteLine($"agent: {thread.CurrentAgentName}");
        output.WriteLine($"messages: {thread.MessageCount}");
        output.WriteLine($"updated: {thread.LastUpdatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        output.WriteLine($"log: {thread.FilePath}");
        output.WriteLine("----------------------");
    }

    private void UseThread(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /thread use <id>");
            return;
        }

        var threadId = string.Join(' ', args[1..]).Trim();
        if (string.IsNullOrEmpty(threadId))
        {
            output.WriteLine("スレッド ID を指定してください。");
            return;
        }

        var alreadyCurrent = string.Equals(threadId, _threadSessionManager.CurrentThreadId, StringComparison.Ordinal);
        if (!_threadSessionManager.TrySwitchToThread(threadId, out var summary, out var error))
        {
            output.WriteLine(error ?? "スレッドの切り替えに失敗しました。");
            return;
        }

        if (alreadyCurrent)
        {
            output.WriteLine($"既にスレッド '{summary!.ThreadId}' を使用中です。");
            return;
        }

        _chatTraceRecorder.RecordThreadChanged(summary!.ThreadId, summary.CurrentAgentName, "thread_use");
        output.WriteLine($"スレッド '{summary.ThreadId}' に切り替えました。");
    }

    private void CreateThread(TextWriter output)
    {
        if (!_threadSessionManager.TryCreateAndSwitchToNewThread(out var summary, out var error))
        {
            output.WriteLine(error ?? "スレッドの作成に失敗しました。");
            return;
        }

        _chatTraceRecorder.RecordThreadChanged(summary!.ThreadId, summary.CurrentAgentName, "thread_new");
        output.WriteLine($"新しいスレッド '{summary!.ThreadId}' に切り替えました。");
    }
}
