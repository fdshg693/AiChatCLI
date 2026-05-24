using AiChatCLI.Commands;
using AutoGen.Core;
using System.Net;
using System.Net.Http;

namespace AiChatCLI.Tests;

public sealed class ToolVisibilityTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public ToolVisibilityTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void AppConfig_DefaultsEnabledBaseToolsWhenUnset()
    {
        var repoRoot = CreateRepoRoot("default-config");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "ApiKey": "test-key"
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var config = new AppConfig(paths);

        Assert.Equal(
            [CommandTools.BaseToolName, MemoryTools.BaseToolName, SubAgentTools.FunctionName],
            config.EnabledBaseTools.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(Path.Combine(repoRoot, "agents.json"), config.AgentsPath);
        Assert.Equal(Path.Combine(repoRoot, "prompts.json"), config.PromptsPath);
        Assert.Equal(Path.Combine(repoRoot, "memory.json"), config.MemoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs"), config.ChatHistoryDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs", "threads"), config.ThreadsDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs", "threads", "subagents"), config.SubAgentThreadsDirectoryPath);
    }

    [Fact]
    public void AppConfig_ReadsEnabledBaseToolsFromJsonArray()
    {
        var repoRoot = CreateRepoRoot("configured-tools");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "ApiKey": "test-key"
              },
              "Tools": {
                "Enabled": ["memory"]
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var config = new AppConfig(paths);

        Assert.Equal([MemoryTools.BaseToolName], config.EnabledBaseTools);
    }

    [Fact]
    public void AppConfig_ReadsConfiguredPathsFromPathsSection()
    {
        var repoRoot = CreateRepoRoot("configured-paths");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "ApiKey": "test-key"
              },
              "Paths": {
                "Agents": "config/agents.custom.json",
                "LegacySystemPrompts": "config/system_prompts.custom.json",
                "Prompts": "config/prompts.custom.json",
                "Memory": "state/memory.custom.json",
                "ChatHistoryDirectory": "artifacts/chat",
                "ThreadsDirectory": "artifacts/threads",
                "SubAgentThreadsDirectory": "artifacts/subagents"
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var config = new AppConfig(paths);

        Assert.Equal(Path.Combine(repoRoot, "config", "agents.custom.json"), config.AgentsPath);
        Assert.Equal(Path.Combine(repoRoot, "config", "system_prompts.custom.json"), config.LegacySystemPromptsPath);
        Assert.Equal(Path.Combine(repoRoot, "config", "prompts.custom.json"), config.PromptsPath);
        Assert.Equal(Path.Combine(repoRoot, "state", "memory.custom.json"), config.MemoryPath);
        Assert.Equal(Path.Combine(repoRoot, "artifacts", "chat"), config.ChatHistoryDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "artifacts", "threads"), config.ThreadsDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "artifacts", "subagents"), config.SubAgentThreadsDirectoryPath);
    }

    [Fact]
    public void AppConfig_FallsBackToLegacyChatHistoryDirectoryWhenPathsValueIsUnset()
    {
        var repoRoot = CreateRepoRoot("legacy-chat-history");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "ApiKey": "test-key"
              },
              "ChatHistory": {
                "Directory": "legacy-logs"
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var config = new AppConfig(paths);

        Assert.Equal(Path.Combine(repoRoot, "legacy-logs"), config.ChatHistoryDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "legacy-logs", "threads"), config.ThreadsDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "legacy-logs", "threads", "subagents"), config.SubAgentThreadsDirectoryPath);
    }

    [Fact]
    public void AppConfig_ReadsApiKeysFromLocalSettingsFile()
    {
        using var _ = new EnvironmentVariableScope("OPENAI_API_KEY", null);
        using var __ = new EnvironmentVariableScope("TAVILY_API_KEY", null);
        var repoRoot = CreateRepoRoot("local-settings");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "Model": "gpt-5.4"
              }
            }
            """);
        File.WriteAllText(
            Path.Combine(repoRoot, AppPaths.DefaultLocalAppSettingsFileName),
            """
            {
              "OpenAI": {
                "ApiKey": "openai-local-key"
              },
              "Tavily": {
                "ApiKey": "tvly-local-key"
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var config = new AppConfig(paths);

        Assert.Equal("openai-local-key", config.ApiKey);
        Assert.Equal("tvly-local-key", config.TavilyApiKey);
        Assert.Equal("gpt-5.4", config.Model);
    }

    [Fact]
    public void AppConfig_ThrowsWhenSearchEnabledWithoutTavilyApiKey()
    {
        using var _ = new EnvironmentVariableScope("TAVILY_API_KEY", null);
        var repoRoot = CreateRepoRoot("missing-tavily-key");
        File.WriteAllText(
            Path.Combine(repoRoot, "appsettings.json"),
            """
            {
              "OpenAI": {
                "ApiKey": "test-key"
              },
              "Tools": {
                "Enabled": ["memory", "sub_agent", "search"]
              }
            }
            """);

        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);

        var ex = Assert.Throws<InvalidOperationException>(() => new AppConfig(paths));
        Assert.Contains("Tavily:ApiKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentToolCatalog_RespectsBaseToolConfig()
    {
        var memoryStore = new MemoryStore(Path.Combine(_tempRoot, "memory.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog(
            new HashSet<string>([MemoryTools.BaseToolName], StringComparer.OrdinalIgnoreCase));
        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterSubAgentTool(CreateSubAgentTools());

        Assert.Equal(
            [MemoryTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.MainAgent));
        Assert.Equal(
            [MemoryTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.SubAgent));
    }

    [Fact]
    public void AgentToolCatalog_HidesSubAgentToolForSubAgentConsumer()
    {
        var memoryStore = new MemoryStore(Path.Combine(_tempRoot, "memory2.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog(
            new HashSet<string>([MemoryTools.BaseToolName, SubAgentTools.FunctionName], StringComparer.OrdinalIgnoreCase));
        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterSubAgentTool(CreateSubAgentTools());

        Assert.Equal(
            [MemoryTools.BaseToolName, SubAgentTools.FunctionName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.MainAgent));
        Assert.Equal(
            [MemoryTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.SubAgent));
    }

    [Fact]
    public void AgentToolCatalog_ExposesSearchToolToMainAndSubAgents()
    {
        var memoryStore = new MemoryStore(Path.Combine(_tempRoot, "memory3.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog(
            new HashSet<string>(
                [MemoryTools.BaseToolName, TavilySearchTools.BaseToolName],
                StringComparer.OrdinalIgnoreCase));
        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterSearchTool(CreateSearchTools());

        Assert.Equal(
            [MemoryTools.BaseToolName, TavilySearchTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.MainAgent));
        Assert.Equal(
            [MemoryTools.BaseToolName, TavilySearchTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.SubAgent));
    }

    [Fact]
    public void AgentToolCatalog_ExposesCommandToolToMainAndSubAgents()
    {
        var toolCatalog = new AgentToolCatalog(
            new HashSet<string>([CommandTools.BaseToolName], StringComparer.OrdinalIgnoreCase));
        toolCatalog.RegisterCommandTool(CreateCommandTools());

        Assert.Equal(
            [CommandTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.MainAgent));
        Assert.Equal(
            [CommandTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(AgentToolConsumer.SubAgent));
    }

    [Fact]
    public void StatusCommand_ListsEnabledToolNamesForMainAndSubAgents()
    {
        var repoRoot = CreateRepoRoot("status-command");
        var promptsPath = Path.Combine(repoRoot, "prompts.json");
        var memoryStore = new MemoryStore(Path.Combine(repoRoot, "memory.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog(
            new HashSet<string>([MemoryTools.BaseToolName, SubAgentTools.FunctionName], StringComparer.OrdinalIgnoreCase));
        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterSubAgentTool(CreateSubAgentTools());

        var agentCatalog = new AgentCatalog(Path.Combine(repoRoot, "agents.json"));
        var agentSelection = new AgentSelection(agentCatalog);
        var templateManager = new PromptTemplateManager(promptsPath);
        using var threadSessionManager = new ThreadSessionManager(
            enabled: false,
            threadDirectoryPath: Path.Combine(repoRoot, "logs", "threads"),
            modelName: "gpt-4o-mini",
            chatService: new StubChatService(),
            agentSelection: agentSelection,
            conversationCodec: new ConversationCodec());
        var command = new StatusCommand(
            "gpt-4o-mini",
            promptsPath,
            memoryStore,
            templateManager,
            agentCatalog,
            agentSelection,
            threadSessionManager,
            toolCatalog);
        using var output = new StringWriter();

        command.Execute([], output);

        var text = output.ToString();
        Assert.Contains(
            $"利用可能 tool (main): {MemoryTools.BaseToolName}, {SubAgentTools.FunctionName}",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            $"利用可能 tool (sub-agent): {MemoryTools.BaseToolName}",
            text,
            StringComparison.Ordinal);
        Assert.Contains($"template 定義ファイル: {promptsPath}", text, StringComparison.Ordinal);
        Assert.Contains($"agent 定義ファイル: {agentCatalog.SourcePath}", text, StringComparison.Ordinal);
        Assert.Contains($"memory 保存ファイル: {memoryStore.FilePath}", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private string CreateRepoRoot(string directoryName)
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, directoryName)).FullName;
        File.WriteAllText(Path.Combine(repoRoot, "AiChatCLI.csproj"), "<Project />");
        return repoRoot;
    }

    private static SubAgentTools CreateSubAgentTools()
    {
        var runner = new SubAgentRunner(
            () => null!,
            new StubTurnExecutor(),
            new ConversationCodec(),
            null,
            "gpt-4o-mini");

        return new SubAgentTools(runner);
    }

    private static TavilySearchTools CreateSearchTools()
    {
        var client = new TavilySearchClient(
            "tvly-test-key",
            new HttpClient(new StubHttpMessageHandler("""
            {
              "query": "test",
              "results": [],
              "response_time": 0.1,
              "request_id": "req_test"
            }
            """)));

        return new TavilySearchTools(client);
    }

    private static CommandTools CreateCommandTools() =>
        new(new StubCommandApprovalPrompt(), new StubCommandExecutor());

    private sealed class StubChatService : IChatService
    {
        public Task<ChatTurnResult> SendAsync(string message) =>
            Task.FromResult(new ChatTurnResult(string.Empty, [], []));

        public void SetAgent(string agentName, string systemPrompt)
        {
        }

        public void RestoreConversation(string agentName, string systemPrompt, IReadOnlyList<ThreadMessageRecord> conversation)
        {
        }
    }

    private sealed class StubTurnExecutor : IAgentTurnExecutor
    {
        public Task<AgentTurnExecution> ExecuteAsync(IAgent agent, List<IMessage> turnHistory)
        {
            var turnResult = new ChatTurnResult(
                string.Empty,
                [],
                [ThreadMessageRecord.CreateText(Role.Assistant, string.Empty, "assistant")]);

            return Task.FromResult(new AgentTurnExecution(turnResult, turnHistory));
        }
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
    }

    private sealed class StubCommandApprovalPrompt : ICommandApprovalPrompt
    {
        public Task<CommandApprovalDecision> RequestApprovalAsync(string command) =>
            Task.FromResult(CommandApprovalDecision.Approve());
    }

    private sealed class StubCommandExecutor : ICommandExecutor
    {
        public Task<CommandExecutionResult> ExecuteAsync(string command, TimeSpan timeout) =>
            Task.FromResult(new CommandExecutionResult(0, string.Empty, string.Empty, false));
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previousValue);
    }
}
