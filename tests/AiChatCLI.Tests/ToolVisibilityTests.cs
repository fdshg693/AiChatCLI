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
    public void AppConfig_UsesDefaultPathsWhenUnset()
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

        Assert.Equal(Path.Combine(repoRoot, "agents.json"), config.AgentsPath);
        Assert.Equal(Path.Combine(repoRoot, "prompts.json"), config.PromptsPath);
        Assert.Equal(Path.Combine(repoRoot, "memory.json"), config.MemoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs"), config.ChatHistoryDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs", "threads"), config.ThreadsDirectoryPath);
        Assert.Equal(Path.Combine(repoRoot, "logs", "threads", "subagents"), config.SubAgentThreadsDirectoryPath);
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
    public void AgentToolCatalog_FiltersConfiguredToolsForMainAndSubAgents()
    {
        var memoryStore = new MemoryStore(Path.Combine(_tempRoot, "memory.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog();
        var enabledTools = new HashSet<string>(
            [MemoryTools.BaseToolName, FileReadTools.BaseToolName, SubAgentTools.FunctionName],
            StringComparer.OrdinalIgnoreCase);

        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterFileReadTool(CreateFileReadTools());
        toolCatalog.RegisterSubAgentTool(CreateSubAgentTools());

        Assert.Equal(
            [MemoryTools.BaseToolName, FileReadTools.BaseToolName, SubAgentTools.FunctionName],
            toolCatalog.GetEnabledToolNames(enabledTools, AgentToolConsumer.MainAgent));
        Assert.Equal(
            [MemoryTools.BaseToolName, FileReadTools.BaseToolName],
            toolCatalog.GetEnabledToolNames(enabledTools, AgentToolConsumer.SubAgent));
    }

    [Fact]
    public void AgentToolCatalog_FindsUnknownToolNames()
    {
        var unknownTools = AgentToolCatalog.FindUnknownToolNames(["memory", "mystery_tool", "another_tool"]);

        Assert.Equal(["another_tool", "mystery_tool"], unknownTools);
    }

    [Fact]
    public void StatusCommand_ListsCurrentAgentAndSubAgentToolNames()
    {
        var repoRoot = CreateRepoRoot("status-command");
        var promptsPath = Path.Combine(repoRoot, "prompts.json");
        File.WriteAllText(
            Path.Combine(repoRoot, "agents.json"),
            """
            {
              "defaults": {
                "systemPromptPrefix": "Shared guidance."
              },
              "agents": {
                "default": {
                  "prompt": "You are a helpful assistant.",
                  "tools": ["memory", "sub_agent", "read_file"]
                },
                "coder": {
                  "prompt": "Write code carefully.",
                  "tools": ["read_file"]
                }
              }
            }
            """);
        var memoryStore = new MemoryStore(Path.Combine(repoRoot, "memory.json"));
        var memoryTools = new MemoryTools(memoryStore);
        var toolCatalog = new AgentToolCatalog();
        toolCatalog.RegisterMemoryTool(memoryTools);
        toolCatalog.RegisterFileReadTool(CreateFileReadTools());
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
            $"利用可能 tool (current agent): {MemoryTools.BaseToolName}, {FileReadTools.BaseToolName}, {SubAgentTools.FunctionName}",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            $"利用可能 tool (current sub-agent): {MemoryTools.BaseToolName}, {FileReadTools.BaseToolName}",
            text,
            StringComparison.Ordinal);
        Assert.Contains("agent 件数: 2", text, StringComparison.Ordinal);
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

    private static CommandTools CreateCommandTools() =>
        new(new StubCommandApprovalPrompt(), new StubCommandExecutor());

    private static FileReadTools CreateFileReadTools() =>
        new(new SessionWorkingDirectory(Path.GetTempPath()), new TextFileReader());

    private sealed class StubChatService : IChatService
    {
        public Task<ChatTurnResult> SendAsync(string message) =>
            Task.FromResult(new ChatTurnResult(string.Empty, [], []));

        public void SetAgent(string agentName, string systemPrompt, IReadOnlySet<string> enabledTools)
        {
        }

        public void RestoreConversation(
            string agentName,
            string systemPrompt,
            IReadOnlySet<string> enabledTools,
            IReadOnlyList<ThreadMessageRecord> conversation)
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
