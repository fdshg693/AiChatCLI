namespace AiChatCLI.Tests;

public sealed class PlaceholderExpansionTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public PlaceholderExpansionTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void PromptTemplateProcessor_ExpandsNestedTemplateReferences()
    {
        var promptsPath = Path.Combine(_tempRoot, "prompts.json");
        File.WriteAllText(
            promptsPath,
            """
            {
              "AA": "Translate the following text to %BB%:",
              "BB": "Japanese"
            }
            """);
        var manager = new PromptTemplateManager(promptsPath);
        var processor = new PromptTemplateProcessor(manager);

        var processed = processor.Process("@AA Hello");

        Assert.Equal("Translate the following text to Japanese: Hello", processed);
    }

    [Fact]
    public void AgentCatalog_ExpandsBuiltInPlaceholders()
    {
        var agentsPath = Path.Combine(_tempRoot, "agents.json");
        File.WriteAllText(
            agentsPath,
            """
            {
              "coder": "Use this environment:\n%SYSTEM_INFO%"
            }
            """);
        var catalog = new AgentCatalog(
            agentsPath,
            builtInPlaceholders: CreateBuiltInPlaceholders());

        Assert.True(catalog.TryGetAgentPrompt("coder", out var prompt));
        Assert.Contains("OS: test-os", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("%SYSTEM_INFO%", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentCatalog_ExpandsSameFileReferencesAfterBuiltIns()
    {
        var agentsPath = Path.Combine(_tempRoot, "agents-nested.json");
        File.WriteAllText(
            agentsPath,
            """
            {
              "base": "Base prompt.\n%SYSTEM_INFO%",
              "coder": "%base%\nWrite code carefully."
            }
            """);
        var catalog = new AgentCatalog(
            agentsPath,
            builtInPlaceholders: CreateBuiltInPlaceholders());

        Assert.True(catalog.TryGetAgentPrompt("coder", out var prompt));
        Assert.Contains("Base prompt.", prompt, StringComparison.Ordinal);
        Assert.Contains("OS: test-os", prompt, StringComparison.Ordinal);
        Assert.Contains("Write code carefully.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentCatalog_LeavesUnknownPlaceholdersUnchanged()
    {
        var agentsPath = Path.Combine(_tempRoot, "agents-unknown.json");
        File.WriteAllText(
            agentsPath,
            """
            {
              "coder": "Keep %UNKNOWN% literal."
            }
            """);
        var catalog = new AgentCatalog(
            agentsPath,
            builtInPlaceholders: CreateBuiltInPlaceholders());

        Assert.True(catalog.TryGetAgentPrompt("coder", out var prompt));
        Assert.Equal("Keep %UNKNOWN% literal.", prompt);
    }

    [Fact]
    public void AgentCatalog_ReloadAgentsFromDisk_ReexpandsPlaceholders()
    {
        var agentsPath = Path.Combine(_tempRoot, "agents-reload.json");
        File.WriteAllText(
            agentsPath,
            """
            {
              "coder": "Before %SYSTEM_INFO%"
            }
            """);
        var catalog = new AgentCatalog(
            agentsPath,
            builtInPlaceholders: CreateBuiltInPlaceholders());

        File.WriteAllText(
            agentsPath,
            """
            {
              "coder": "After %SYSTEM_INFO%"
            }
            """);

        Assert.True(catalog.ReloadAgentsFromDisk());
        Assert.True(catalog.TryGetAgentPrompt("coder", out var prompt));
        Assert.Contains("After OS: test-os", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Before", prompt, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static IReadOnlyDictionary<string, string> CreateBuiltInPlaceholders()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentBuiltInPlaceholders.SystemInfoKey] = "OS: test-os"
        };
    }
}
