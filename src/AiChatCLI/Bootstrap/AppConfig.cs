using Microsoft.Extensions.Configuration;

namespace AiChatCLI;

internal class AppConfig
{
    public string ApiKey { get; }
    public string? TavilyApiKey { get; }
    public string Model { get; }
    public int MaxTemplateDepth { get; }
    public bool ChatHistoryEnabled { get; }
    public string AgentsPath { get; }
    public string PromptsPath { get; }
    public string MemoryPath { get; }
    public string ChatHistoryDirectoryPath { get; }
    public string ThreadsDirectoryPath { get; }
    public string SubAgentThreadsDirectoryPath { get; }

    public AppConfig(AppPaths paths)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(paths.ContentRoot)
            .AddJsonFile(Path.GetRelativePath(paths.ContentRoot, paths.AppSettingsPath), optional: true)
            .AddJsonFile(Path.GetRelativePath(paths.ContentRoot, paths.LocalAppSettingsPath), optional: true)
            .AddEnvironmentVariables()
            .Build();

        ApiKey = ReadTrimmedValue(config, "OpenAI:ApiKey")
            ?? ReadTrimmedValue(config, "OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                $"OpenAI APIキーが見つかりません。{AppPaths.DefaultLocalAppSettingsFileName} の OpenAI:ApiKey または環境変数 OPENAI_API_KEY を設定してください。");

        TavilyApiKey = ReadTrimmedValue(config, "Tavily:ApiKey")
            ?? ReadTrimmedValue(config, "TAVILY_API_KEY");

        Model = config["OpenAI:Model"] ?? "gpt-4o-mini";

        MaxTemplateDepth = int.TryParse(config["Template:MaxDepth"], out var depth) ? depth : 10;

        ChatHistoryEnabled = !bool.TryParse(config["ChatHistory:Enabled"], out var chatHistoryEnabled) ||
                             chatHistoryEnabled;
        AgentsPath = paths.ResolveConfiguredPath(ReadTrimmedValue(config, "Paths:Agents"), AppPaths.DefaultAgentsFileName);
        PromptsPath = paths.ResolveConfiguredPath(ReadTrimmedValue(config, "Paths:Prompts"), AppPaths.DefaultPromptsFileName);
        MemoryPath = paths.ResolveConfiguredPath(ReadTrimmedValue(config, "Paths:Memory"), AppPaths.DefaultMemoryFileName);

        var configuredChatHistoryDirectory = ReadTrimmedValue(config, "Paths:ChatHistoryDirectory")
            ?? ReadTrimmedValue(config, "ChatHistory:Directory");
        ChatHistoryDirectoryPath = paths.ResolveConfiguredPath(
            configuredChatHistoryDirectory,
            AppPaths.DefaultChatHistoryDirectoryName);

        var configuredThreadsDirectory = ReadTrimmedValue(config, "Paths:ThreadsDirectory");
        ThreadsDirectoryPath = configuredThreadsDirectory is null
            ? Path.GetFullPath(Path.Combine(ChatHistoryDirectoryPath, AppPaths.DefaultThreadsDirectoryName))
            : paths.ResolvePath(configuredThreadsDirectory);

        var configuredSubAgentThreadsDirectory = ReadTrimmedValue(config, "Paths:SubAgentThreadsDirectory");
        SubAgentThreadsDirectoryPath = configuredSubAgentThreadsDirectory is null
            ? Path.GetFullPath(Path.Combine(ThreadsDirectoryPath, AppPaths.DefaultSubAgentThreadsDirectoryName))
            : paths.ResolvePath(configuredSubAgentThreadsDirectory);
    }

    private static string? ReadTrimmedValue(IConfiguration config, string key)
    {
        var value = config[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
