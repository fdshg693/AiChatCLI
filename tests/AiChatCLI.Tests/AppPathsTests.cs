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
    public void Discover_UsesMarkerDirectoryAsContentRoot()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo")).FullName;
        File.WriteAllText(Path.Combine(repoRoot, "AiChatCLI.csproj"), "<Project />");
        var startDirectory = Directory.CreateDirectory(Path.Combine(repoRoot, "src", "AiChatCLI", "bin", "Debug")).FullName;

        var paths = AppPaths.Discover("AiChatCLI.csproj", startDirectory, Path.Combine(_tempRoot, "fallback"));

        Assert.Equal(Path.GetFullPath(repoRoot), paths.ContentRoot);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(repoRoot, "logs", "threads")),
            paths.ResolveConfiguredPath(Path.Combine("logs", "threads"), AppPaths.DefaultChatHistoryDirectoryName));
    }

    [Fact]
    public void Discover_FallsBackWhenMarkerIsMissing()
    {
        var startDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "start")).FullName;
        var fallbackDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "fallback")).FullName;

        var paths = AppPaths.Discover("AiChatCLI.csproj", startDirectory, fallbackDirectory);

        Assert.Equal(Path.GetFullPath(fallbackDirectory), paths.ContentRoot);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(fallbackDirectory, "logs")),
            paths.ResolveConfiguredPath(null, AppPaths.DefaultChatHistoryDirectoryName));
    }

    [Fact]
    public void ResolveConfiguredPath_UsesDefaultRelativePathWhenUnset()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo-defaults")).FullName;
        File.WriteAllText(Path.Combine(repoRoot, "AiChatCLI.csproj"), "<Project />");
        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);

        var resolved = paths.ResolveConfiguredPath(null, Path.Combine("data", "memory.json"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(repoRoot, "data", "memory.json")),
            resolved);
    }

    [Fact]
    public void ResolvePath_PreservesAbsolutePath()
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "repo-absolute")).FullName;
        File.WriteAllText(Path.Combine(repoRoot, "AiChatCLI.csproj"), "<Project />");
        var paths = AppPaths.Discover("AiChatCLI.csproj", repoRoot, repoRoot);
        var absolutePath = Path.GetFullPath(Path.Combine(_tempRoot, "shared", "memory.json"));

        var resolved = paths.ResolvePath(absolutePath);

        Assert.Equal(absolutePath, resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
