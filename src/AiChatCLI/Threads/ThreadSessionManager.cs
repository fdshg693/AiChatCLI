namespace AiChatCLI;

internal sealed class ThreadSessionManager : IDisposable
{
    private readonly IChatService _chatService;
    private readonly AgentSelection _agentSelection;
    private readonly string _modelName;
    private readonly string _sessionId = $"session_{Guid.NewGuid():N}";
    private readonly ThreadProjector _projector;
    private readonly ThreadRecorder? _recorder;
    private readonly ThreadRepository? _repository;
    private bool _disposed;

    public ThreadSessionManager(
        bool enabled,
        string threadDirectoryPath,
        string modelName,
        IChatService chatService,
        AgentSelection agentSelection,
        ConversationCodec conversationCodec)
    {
        IsEnabled = enabled;
        _modelName = modelName;
        _chatService = chatService;
        _agentSelection = agentSelection;
        _projector = new ThreadProjector(conversationCodec);

        if (enabled)
        {
            _repository = new ThreadRepository(threadDirectoryPath);
            _recorder = new ThreadRecorder(_repository, conversationCodec, _sessionId);
        }
    }

    public bool IsEnabled { get; }
    public string? CurrentThreadId { get; private set; }

    public void Initialize()
    {
        if (!IsEnabled || _repository is null || CurrentThreadId is not null)
            return;

        var threadId = _repository.CreateThread(_modelName, _agentSelection.CurrentName, _agentSelection.CurrentPrompt);
        _chatService.RestoreConversation(
            _agentSelection.CurrentName,
            _agentSelection.CurrentPrompt,
            _agentSelection.CurrentTools,
            []);
        CurrentThreadId = threadId;
        _recorder!.RecordSessionAttached(
            threadId,
            _modelName,
            _agentSelection.CurrentName,
            _agentSelection.CurrentPrompt,
            "startup");
    }

    public IReadOnlyList<ThreadSummary> ListThreads() =>
        !IsEnabled || _repository is null
            ? []
            : _repository.ListThreads(_projector);

    public ThreadSummary? GetCurrentThreadSummary()
    {
        if (!IsEnabled || _repository is null || CurrentThreadId is null)
            return null;

        return LoadThreadSummary(CurrentThreadId);
    }

    public bool TryCreateAndSwitchToNewThread(out ThreadSummary? summary, out string? error)
    {
        summary = null;
        error = null;

        if (!EnsureEnabled(out error))
            return false;

        try
        {
            var nextAgentName = _agentSelection.CurrentName;
            var nextSystemPrompt = _agentSelection.CurrentPrompt;
            var newThreadId = _repository!.CreateThread(_modelName, nextAgentName, nextSystemPrompt);

            DetachCurrent("thread_switch");
            _chatService.RestoreConversation(
                nextAgentName,
                nextSystemPrompt,
                _agentSelection.CurrentTools,
                []);
            CurrentThreadId = newThreadId;
            _recorder!.RecordSessionAttached(
                newThreadId,
                _modelName,
                nextAgentName,
                nextSystemPrompt,
                "thread_new");

            summary = LoadThreadSummary(newThreadId);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySwitchToThread(string threadId, out ThreadSummary? summary, out string? error)
    {
        summary = null;
        error = null;

        if (!EnsureEnabled(out error))
            return false;

        if (string.Equals(CurrentThreadId, threadId, StringComparison.Ordinal))
        {
            summary = LoadThreadSummary(threadId);
            return true;
        }

        if (!_repository!.ThreadExists(threadId))
        {
            error = $"スレッド '{threadId}' は見つかりません。";
            return false;
        }

        try
        {
            var snapshot = LoadThreadSnapshot(threadId);
            DetachCurrent("thread_switch");
            _agentSelection.SetCurrent(snapshot.CurrentAgentName, snapshot.CurrentSystemPrompt);
            _chatService.RestoreConversation(
                snapshot.CurrentAgentName,
                snapshot.CurrentSystemPrompt,
                _agentSelection.CurrentTools,
                snapshot.Conversation);
            CurrentThreadId = snapshot.ThreadId;
            _recorder!.RecordSessionAttached(
                snapshot.ThreadId,
                _modelName,
                snapshot.CurrentAgentName,
                snapshot.CurrentSystemPrompt,
                "thread_use");

            summary = LoadThreadSummary(snapshot.ThreadId);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public void RecordUserInput(string rawInput, string agentName)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordUserInput(CurrentThreadId, agentName, rawInput);
    }

    public void RecordPromptTransformation(string rawInput, string processedInput, string agentName)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordPromptTransformation(CurrentThreadId, agentName, rawInput, processedInput);
    }

    public void RecordModelRequest(string processedInput, string agentName)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordModelRequest(CurrentThreadId, agentName, processedInput);
    }

    public void RecordTurn(ChatTurnResult turnResult, string agentName)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordTurn(CurrentThreadId, agentName, turnResult);
    }

    public void RecordAgentChange(string reason)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordAgentChange(
            CurrentThreadId,
            _agentSelection.CurrentName,
            _agentSelection.CurrentPrompt,
            reason);
    }

    public void Shutdown(string reason = "exit")
    {
        if (_disposed)
            return;

        DetachCurrent(reason);
        _disposed = true;
    }

    public void Dispose()
    {
        Shutdown("dispose");
        GC.SuppressFinalize(this);
    }

    private bool EnsureEnabled(out string? error)
    {
        if (IsEnabled && _repository is not null)
        {
            error = null;
            return true;
        }

        error = "会話履歴ログが無効のため thread 操作は利用できません。";
        return false;
    }

    private void DetachCurrent(string reason)
    {
        if (CurrentThreadId is null || _recorder is null)
            return;

        _recorder.RecordSessionDetached(CurrentThreadId, reason);
        CurrentThreadId = null;
    }

    private ThreadSnapshot LoadThreadSnapshot(string threadId)
    {
        var filePath = _repository!.GetThreadFilePath(threadId);
        var events = _repository.ReadEvents(threadId);
        return _projector.Project(filePath, events);
    }

    private ThreadSummary LoadThreadSummary(string threadId) =>
        _projector.CreateSummary(LoadThreadSnapshot(threadId));
}
