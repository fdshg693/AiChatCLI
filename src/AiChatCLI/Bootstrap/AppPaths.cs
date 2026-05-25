namespace AiChatCLI;

internal sealed class AppPaths
{
    public const string DefaultLocalAppSettingsFileName = "appsettings.local.json";
    public const string DefaultLocalAppSettingsExampleFileName = "appsettings.local.example.json";
    public const string DefaultSettingsDirectoryName = ".ai_chat";
    public const string DefaultSettingsFileName = "settings.json";
    public const string DefaultAgentsFileName = "agents.json";
    public const string DefaultMemoryFileName = "memory.json";
    public const string DefaultPromptsFileName = "prompts.json";
    public const string DefaultSkillsDirectoryName = "skills";
    public const string DefaultChatHistoryDirectoryName = "logs";
    public const string DefaultThreadsDirectoryName = "threads";
    public const string DefaultSubAgentThreadsDirectoryName = "subagents";

    private AppPaths(string projectRoot, string repoRoot)
    {
        ProjectRoot = Path.GetFullPath(projectRoot);
        RepoRoot = Path.GetFullPath(repoRoot);
    }

    public string ContentRoot => ProjectRoot;

    public string ProjectRoot { get; }

    public string RepoRoot { get; }

    public string SettingsDirectoryPath => Path.GetFullPath(Path.Combine(RepoRoot, DefaultSettingsDirectoryName));

    public string SettingsBaseDirectoryPath => SettingsDirectoryPath;

    public string SettingsPath => Path.GetFullPath(Path.Combine(SettingsDirectoryPath, DefaultSettingsFileName));

    public string LocalAppSettingsPath => Path.GetFullPath(Path.Combine(ProjectRoot, DefaultLocalAppSettingsFileName));

    public static AppPaths Discover(
        string markerFileName,
        string? startDirectory = null,
        string? fallbackDirectory = null)
    {
        var projectRoot = TryFindProjectRoot(markerFileName, startDirectory)
            ?? Path.GetFullPath(fallbackDirectory ?? Directory.GetCurrentDirectory());
        var repoRoot = TryFindRepoRoot(projectRoot)
            ?? TryGetConventionalRepoRoot(projectRoot)
            ?? projectRoot;

        return new AppPaths(projectRoot, repoRoot);
    }

    public string ResolveConfiguredSettingsPath(string? configuredPath, string defaultRelativePath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath.Trim();

        return ResolveSettingsPath(candidate);
    }

    public string ResolveProjectPath(string path) =>
        ResolvePath(path, ProjectRoot, nameof(path));

    public string ResolveSettingsPath(string path) =>
        ResolvePath(path, SettingsBaseDirectoryPath, nameof(path));

    private static string ResolvePath(string path, string baseDirectoryPath, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path を指定してください。", paramName);

        var trimmed = path.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(baseDirectoryPath, trimmed));
    }

    private static string? TryFindProjectRoot(string markerFileName, string? startDirectory)
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

    private static string? TryFindRepoRoot(string projectRoot)
    {
        var current = new DirectoryInfo(Path.GetFullPath(projectRoot));
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            var settingsPath = Path.Combine(
                current.FullName,
                DefaultSettingsDirectoryName,
                DefaultSettingsFileName);
            if (Directory.Exists(gitPath) || File.Exists(gitPath) || File.Exists(settingsPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static string? TryGetConventionalRepoRoot(string projectRoot)
    {
        var current = new DirectoryInfo(Path.GetFullPath(projectRoot));
        if (current.Parent is null ||
            !string.Equals(current.Parent.Name, "src", StringComparison.OrdinalIgnoreCase) ||
            current.Parent.Parent is null)
        {
            return null;
        }

        return current.Parent.Parent.FullName;
    }
}
