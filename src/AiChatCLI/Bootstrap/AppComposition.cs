using AiChatCLI.Commands;

namespace AiChatCLI;

internal sealed class AppComposition : IDisposable
{
    private readonly ChatHistoryLogger _chatHistory;
    private readonly ThreadSessionManager _threadSessionManager;
    private readonly HttpClient? _searchHttpClient;
    private bool _disposed;

    private AppComposition(
        AppConfig config,
        ChatLoop chatLoop,
        ChatHistoryLogger chatHistory,
        ThreadSessionManager threadSessionManager,
        HttpClient? searchHttpClient)
    {
        Config = config;
        ChatLoop = chatLoop;
        _chatHistory = chatHistory;
        _threadSessionManager = threadSessionManager;
        _searchHttpClient = searchHttpClient;
    }

    public AppConfig Config { get; }

    public ChatLoop ChatLoop { get; }

    public ThreadSessionManager ThreadSessionManager => _threadSessionManager;

    public static AppComposition Create(AppPaths paths)
    {
        ThreadSessionManager? threadSessionManager = null;
        ChatHistoryLogger? chatHistory = null;
        HttpClient? searchHttpClient = null;

        try
        {
            var config = new AppConfig(paths);
            var sessionWorkingDirectory = new SessionWorkingDirectory();
            var agentCatalog = new AgentCatalog(
                config.AgentsPath,
                AgentBuiltInPlaceholders.CreateResolver(sessionWorkingDirectory),
                config.MaxTemplateDepth);
            var agentSelection = new AgentSelection(agentCatalog);
            ValidateConfiguredAgentTools(agentCatalog);
            var memoryStore = new MemoryStore(config.MemoryPath);
            var memoryTools = new MemoryTools(memoryStore);
            var fileReadTools = new FileReadTools(sessionWorkingDirectory, new TextFileReader());
            var commandTools = new CommandTools(
                new ConsoleCommandApprovalPrompt(),
                new LocalCommandExecutor());
            var toolCatalog = new AgentToolCatalog();
            toolCatalog.RegisterMemoryTool(memoryTools);
            toolCatalog.RegisterFileReadTool(fileReadTools);
            toolCatalog.RegisterCommandTool(commandTools);
            var searchToolEnabled = agentCatalog
                .GetAgents()
                .Any(agent => agent.Value.EnabledTools.Contains(TavilySearchTools.BaseToolName));
            if (searchToolEnabled)
            {
                if (string.IsNullOrWhiteSpace(config.TavilyApiKey))
                {
                    throw new InvalidOperationException(
                        $"search ツールを有効にするには {AppPaths.DefaultLocalAppSettingsFileName} の Tavily:ApiKey または環境変数 TAVILY_API_KEY を設定してください。");
                }

                searchHttpClient = new HttpClient();
                toolCatalog.RegisterSearchTool(new TavilySearchTools(
                    new TavilySearchClient(config.TavilyApiKey!, searchHttpClient)));
            }

            var conversationCodec = new ConversationCodec();
            var turnExecutor = new AgentTurnExecutor(conversationCodec);
            var agentFactory = new OpenAIAgentFactory(
                config.ApiKey,
                config.Model,
                toolCatalog);
            var subAgentRepository = config.ChatHistoryEnabled
                ? ThreadRepository.CreateSubAgentRepository(config.SubAgentThreadsDirectoryPath)
                : null;
            var subAgentRunner = new SubAgentRunner(
                () => agentFactory.CreateSubAgent(
                    SubAgentRunner.AgentName,
                    SubAgentRunner.SystemPrompt,
                    agentSelection.CurrentTools),
                turnExecutor,
                conversationCodec,
                subAgentRepository,
                config.Model);
            toolCatalog.RegisterSubAgentTool(new SubAgentTools(subAgentRunner));
            var chatService = new OpenAIChatService(
                agentFactory,
                conversationCodec,
                turnExecutor,
                agentSelection.CurrentName,
                agentSelection.CurrentPrompt,
                agentSelection.CurrentTools);
            var templateManager = new PromptTemplateManager(config.PromptsPath);
            var templateProcessor = new PromptTemplateProcessor(templateManager, config.MaxTemplateDepth);
            var promptReader = new InteractivePromptReader(templateManager);
            threadSessionManager = new ThreadSessionManager(
                config.ChatHistoryEnabled,
                config.ThreadsDirectoryPath,
                config.Model,
                chatService,
                agentSelection,
                conversationCodec);

            var slashCommandHandler = new SlashCommandHandler();
            slashCommandHandler.Register(new StatusCommand(
                config.Model,
                config.PromptsPath,
                memoryStore,
                templateManager,
                agentCatalog,
                agentSelection,
                threadSessionManager,
                toolCatalog));
            slashCommandHandler.Register(new AgentCommand(
                agentCatalog,
                agentSelection,
                chatService,
                threadSessionManager));
            slashCommandHandler.Register(new ThreadCommand(threadSessionManager));
            slashCommandHandler.Register(new PromptCommand(
                templateManager));
            slashCommandHandler.Register(new HelpCommand(slashCommandHandler));

            chatHistory = new ChatHistoryLogger(config.ChatHistoryEnabled, config.ChatHistoryDirectoryPath);
            var chatTurnPipeline = new ChatTurnPipeline(
                chatService,
                agentSelection,
                templateProcessor,
                slashCommandHandler,
                chatHistory,
                threadSessionManager);
            var chatLoop = new ChatLoop(
                agentSelection,
                chatHistory,
                promptReader,
                chatTurnPipeline);

            var composition = new AppComposition(
                config,
                chatLoop,
                chatHistory,
                threadSessionManager,
                searchHttpClient);
            chatHistory = null;
            threadSessionManager = null;
            searchHttpClient = null;

            return composition;
        }
        finally
        {
            chatHistory?.Dispose();
            threadSessionManager?.Dispose();
            searchHttpClient?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _chatHistory.Dispose();
        _threadSessionManager.Dispose();
        _searchHttpClient?.Dispose();
        _disposed = true;
    }

    private static void ValidateConfiguredAgentTools(AgentCatalog agentCatalog)
    {
        foreach (var (agentName, definition) in agentCatalog.GetAgents())
        {
            var unknownTools = AgentToolCatalog.FindUnknownToolNames(definition.EnabledTools);
            if (unknownTools.Count == 0)
                continue;

            throw new InvalidOperationException(
                $"agents.json の agents.{agentName}.tools に未対応の tool 名があります: {string.Join(", ", unknownTools)}");
        }
    }
}
