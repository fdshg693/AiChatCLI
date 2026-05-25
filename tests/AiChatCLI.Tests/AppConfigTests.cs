namespace AiChatCLI.Tests;

public sealed class AppConfigTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public AppConfigTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void LegacyChatHistoryFlag_DisablesAllLoggingByDefault()
    {
        using var _ = new EnvironmentVariableScope();
        var paths = CreatePaths(
            "legacy-disabled",
            """
            {
              "ChatHistory": {
                "Enabled": false
              }
            }
            """);

        var config = new AppConfig(paths);

        Assert.False(config.TranscriptLoggingEnabled);
        Assert.False(config.ThreadLoggingEnabled);
        Assert.False(config.SubAgentThreadLoggingEnabled);
    }

    [Fact]
    public void LoggingFlags_CanOverrideLegacyChatHistoryFlagIndependently()
    {
        using var _ = new EnvironmentVariableScope();
        var paths = CreatePaths(
            "logging-overrides",
            """
            {
              "ChatHistory": {
                "Enabled": false
              },
              "Logging": {
                "TranscriptEnabled": true,
                "ThreadEnabled": false,
                "SubAgentThreadEnabled": true
              }
            }
            """);

        var config = new AppConfig(paths);

        Assert.True(config.TranscriptLoggingEnabled);
        Assert.False(config.ThreadLoggingEnabled);
        Assert.True(config.SubAgentThreadLoggingEnabled);
    }

    private AppPaths CreatePaths(string repoName, string settingsJson)
    {
        var repoRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, repoName)).FullName;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        var settingsDirectory = Directory.CreateDirectory(Path.Combine(repoRoot, AppPaths.DefaultSettingsDirectoryName)).FullName;
        var projectRoot = Directory.CreateDirectory(Path.Combine(repoRoot, "src", "AiChatCLI")).FullName;

        File.WriteAllText(Path.Combine(projectRoot, "AiChatCLI.csproj"), "<Project />");
        File.WriteAllText(
            Path.Combine(projectRoot, AppPaths.DefaultLocalAppSettingsFileName),
            """
            {
              "OpenAI": {
                "ApiKey": "test-api-key"
              }
            }
            """);
        File.WriteAllText(Path.Combine(settingsDirectory, AppPaths.DefaultSettingsFileName), settingsJson);

        return AppPaths.Discover("AiChatCLI.csproj", projectRoot, repoRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private static readonly string[] Keys =
        [
            "OPENAI_API_KEY",
            "ChatHistory__Enabled",
            "Logging__TranscriptEnabled",
            "Logging__ThreadEnabled",
            "Logging__SubAgentThreadEnabled"
        ];

        private readonly Dictionary<string, string?> _previousValues = new(StringComparer.Ordinal);

        public EnvironmentVariableScope()
        {
            foreach (var key in Keys)
            {
                _previousValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
