using Microsoft.Extensions.Configuration;

namespace AiChatCLI;

internal class AppConfig
{
    public string ApiKey { get; }
    public string? TavilyApiKey { get; }
    public string Model { get; }
    public int MaxTemplateDepth { get; }
    public bool ChatHistoryEnabled { get; }
    public bool TranscriptLoggingEnabled { get; }
    public bool ThreadLoggingEnabled { get; }
    public bool SubAgentThreadLoggingEnabled { get; }
    public string AgentsPath { get; }
    public string PromptsPath { get; }
    public string MemoryPath { get; }
    public string SkillsDirectoryPath { get; }
    public string ChatHistoryDirectoryPath { get; }
    public string ThreadsDirectoryPath { get; }
    public string SubAgentThreadsDirectoryPath { get; }

    public AppConfig(AppPaths paths)
    {
        var secretConfig = new ConfigurationBuilder()
            .SetBasePath(paths.ProjectRoot)
            .AddJsonFile(Path.GetRelativePath(paths.ProjectRoot, paths.LocalAppSettingsPath), optional: true)
            .AddEnvironmentVariables()
            .Build();
        var settingsConfig = new ConfigurationBuilder()
            .SetBasePath(paths.RepoRoot)
            .AddJsonFile(Path.GetRelativePath(paths.RepoRoot, paths.SettingsPath), optional: true)
            .AddEnvironmentVariables()
            .Build();

        ApiKey = ReadTrimmedValue(secretConfig, "OpenAI:ApiKey")
            ?? ReadTrimmedValue(secretConfig, "OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                $"OpenAI APIキーが見つかりません。{AppPaths.DefaultLocalAppSettingsFileName} の OpenAI:ApiKey または環境変数 OPENAI_API_KEY を設定してください。");

        TavilyApiKey = ReadTrimmedValue(secretConfig, "Tavily:ApiKey")
            ?? ReadTrimmedValue(secretConfig, "TAVILY_API_KEY");

        Model = settingsConfig["OpenAI:Model"] ?? "gpt-4o-mini";

        MaxTemplateDepth = int.TryParse(settingsConfig["Template:MaxDepth"], out var depth) ? depth : 10;

        ChatHistoryEnabled = !bool.TryParse(settingsConfig["ChatHistory:Enabled"], out var chatHistoryEnabled) ||
                             chatHistoryEnabled;
        TranscriptLoggingEnabled = ReadBooleanValue(settingsConfig, "Logging:TranscriptEnabled", ChatHistoryEnabled);
        ThreadLoggingEnabled = ReadBooleanValue(settingsConfig, "Logging:ThreadEnabled", ChatHistoryEnabled);
        SubAgentThreadLoggingEnabled = ReadBooleanValue(settingsConfig, "Logging:SubAgentThreadEnabled", ThreadLoggingEnabled);
        AgentsPath = paths.ResolveConfiguredSettingsPath(ReadTrimmedValue(settingsConfig, "Paths:Agents"), AppPaths.DefaultAgentsFileName);
        PromptsPath = paths.ResolveConfiguredSettingsPath(ReadTrimmedValue(settingsConfig, "Paths:Prompts"), AppPaths.DefaultPromptsFileName);
        MemoryPath = paths.ResolveConfiguredSettingsPath(ReadTrimmedValue(settingsConfig, "Paths:Memory"), AppPaths.DefaultMemoryFileName);
        SkillsDirectoryPath = paths.ResolveConfiguredSettingsPath(
            ReadTrimmedValue(settingsConfig, "Paths:SkillsDirectory"),
            AppPaths.DefaultSkillsDirectoryName);

        var configuredChatHistoryDirectory = ReadTrimmedValue(settingsConfig, "Paths:ChatHistoryDirectory");
        ChatHistoryDirectoryPath = paths.ResolveConfiguredSettingsPath(
            configuredChatHistoryDirectory,
            AppPaths.DefaultChatHistoryDirectoryName);

        var configuredThreadsDirectory = ReadTrimmedValue(settingsConfig, "Paths:ThreadsDirectory");
        ThreadsDirectoryPath = configuredThreadsDirectory is null
            ? Path.GetFullPath(Path.Combine(ChatHistoryDirectoryPath, AppPaths.DefaultThreadsDirectoryName))
            : paths.ResolveSettingsPath(configuredThreadsDirectory);

        var configuredSubAgentThreadsDirectory = ReadTrimmedValue(settingsConfig, "Paths:SubAgentThreadsDirectory");
        SubAgentThreadsDirectoryPath = configuredSubAgentThreadsDirectory is null
            ? Path.GetFullPath(Path.Combine(ThreadsDirectoryPath, AppPaths.DefaultSubAgentThreadsDirectoryName))
            : paths.ResolveSettingsPath(configuredSubAgentThreadsDirectory);
    }

    private static string? ReadTrimmedValue(IConfiguration config, string key)
    {
        var value = config[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ReadBooleanValue(IConfiguration config, string key, bool defaultValue) =>
        bool.TryParse(config[key], out var value) ? value : defaultValue;

}
