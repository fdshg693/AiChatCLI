namespace AiChatCLI;

internal sealed class AppPaths
{
    public const string DefaultAppSettingsFileName = "appsettings.json";
    public const string DefaultLocalAppSettingsFileName = "appsettings.local.json";
    public const string DefaultLocalAppSettingsExampleFileName = "appsettings.local.example.json";
    public const string DefaultAgentsFileName = "agents.json";
    public const string DefaultLegacySystemPromptsFileName = "system_prompts.json";
    public const string DefaultMemoryFileName = "memory.json";
    public const string DefaultPromptsFileName = "prompts.json";
    public const string DefaultChatHistoryDirectoryName = "logs";
    public const string DefaultThreadsDirectoryName = "threads";
    public const string DefaultSubAgentThreadsDirectoryName = "subagents";

    private AppPaths(string contentRoot)
    {
        ContentRoot = contentRoot;
    }

    public string ContentRoot { get; }

    public string AppSettingsPath => ResolveConfiguredPath(null, DefaultAppSettingsFileName);

    public string LocalAppSettingsPath => ResolveConfiguredPath(null, DefaultLocalAppSettingsFileName);

    public string AgentsPath => ResolveConfiguredPath(null, DefaultAgentsFileName);

    public string LegacySystemPromptsPath => ResolveConfiguredPath(null, DefaultLegacySystemPromptsFileName);

    public string MemoryPath => ResolveConfiguredPath(null, DefaultMemoryFileName);

    public string PromptsPath => ResolveConfiguredPath(null, DefaultPromptsFileName);

    public string DefaultChatHistoryDirectory => ResolveConfiguredPath(null, DefaultChatHistoryDirectoryName);

    public static AppPaths Discover(
        string markerFileName,
        string? startDirectory = null,
        string? fallbackDirectory = null)
    {
        var contentRoot = TryFindContentRoot(markerFileName, startDirectory)
            ?? Path.GetFullPath(fallbackDirectory ?? Directory.GetCurrentDirectory());

        return new AppPaths(contentRoot);
    }

    public string ResolveConfiguredPath(string? configuredPath, string defaultRelativePath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath.Trim();

        return ResolvePath(candidate);
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path を指定してください。", nameof(path));

        var trimmed = path.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(ContentRoot, trimmed));
    }

    private static string? TryFindContentRoot(string markerFileName, string? startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory ?? AppContext.BaseDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, markerFileName)))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
