namespace AiChatCLI.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public AppPathsTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void Discover_UsesMarkerDirectoryAsProjectRootAndFindsRepoRoot()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, AppPaths.DefaultSettingsDirectoryName));
        var projectRoot = Directory.CreateDirectory(Path.Combine(repoRoot, "src", "AiChatCLI")).FullName;
        File.WriteAllText(Path.Combine(projectRoot, "AiChatCLI.csproj"), "<Project />");
        var startDirectory = Directory.CreateDirectory(Path.Combine(projectRoot, "bin", "Debug")).FullName;

        var paths = AppPaths.Discover("AiChatCLI.csproj", startDirectory, Path.Combine(_tempRoot, "fallback"));

        Assert.Equal(Path.GetFullPath(projectRoot), paths.ProjectRoot);
        Assert.Equal(Path.GetFullPath(repoRoot), paths.RepoRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(repoRoot, AppPaths.DefaultSettingsDirectoryName)), paths.SettingsBaseDirectoryPath);
        Assert.Equal(Path.GetFullPath(projectRoot), paths.ContentRoot);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(repoRoot, AppPaths.DefaultSettingsDirectoryName, "logs", "threads")),
            paths.ResolveConfiguredSettingsPath(Path.Combine("logs", "threads"), AppPaths.DefaultChatHistoryDirectoryName));
    }

    [Fact]
    public void Discover_FallsBackWhenMarkerIsMissing()
    {
        var startDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "start")).FullName;
        var fallbackDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "fallback")).FullName;

        var paths = AppPaths.Discover("AiChatCLI.csproj", startDirectory, fallbackDirectory);

        Assert.Equal(Path.GetFullPath(fallbackDirectory), paths.ProjectRoot);
        Assert.Equal(Path.GetFullPath(fallbackDirectory), paths.RepoRoot);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(fallbackDirectory, AppPaths.DefaultSettingsDirectoryName, "logs")),
            paths.ResolveConfiguredSettingsPath(null, AppPaths.DefaultChatHistoryDirectoryName));
    }

    [Fact]
    public void ResolveConfiguredSettingsPath_UsesDefaultRelativePathWhenUnset()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo-defaults")).FullName;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var projectRoot = Directory.CreateDirectory(Path.Combine(repoRoot, "src", "AiChatCLI")).FullName;
        File.WriteAllText(Path.Combine(projectRoot, "AiChatCLI.csproj"), "<Project />");
        var paths = AppPaths.Discover("AiChatCLI.csproj", projectRoot, repoRoot);

        var resolved = paths.ResolveConfiguredSettingsPath(null, Path.Combine("data", "memory.json"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(repoRoot, AppPaths.DefaultSettingsDirectoryName, "data", "memory.json")),
            resolved);
    }

    [Fact]
    public void ResolveSettingsPath_PreservesAbsolutePath()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo-absolute")).FullName;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var projectRoot = Directory.CreateDirectory(Path.Combine(repoRoot, "src", "AiChatCLI")).FullName;
        File.WriteAllText(Path.Combine(projectRoot, "AiChatCLI.csproj"), "<Project />");
        var paths = AppPaths.Discover("AiChatCLI.csproj", projectRoot, repoRoot);
        var absolutePath = Path.GetFullPath(Path.Combine(_tempRoot, "shared", "memory.json"));

        var resolved = paths.ResolveSettingsPath(absolutePath);

        Assert.Equal(absolutePath, resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
