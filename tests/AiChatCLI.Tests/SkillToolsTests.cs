using System.Text.Json;

namespace AiChatCLI.Tests;

public sealed class SkillToolsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public SkillToolsTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task Skill_ReadsSkillContentAndAbsolutePath()
    {
        var skillFilePath = WriteSkill(
            "reviewer",
            """
            ---
            name: reviewer
            description: Review code with a bug-finding focus.
            ---
            # Reviewer

            Look for bugs, regressions, and missing tests.
            """);
        var tools = CreateTools();

        var result = await tools.skill("reviewer");

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("reviewer", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "Review code with a bug-finding focus.",
            json.RootElement.GetProperty("description").GetString());
        Assert.Equal(Path.GetFullPath(skillFilePath), json.RootElement.GetProperty("path").GetString());
        Assert.Equal(
            Path.GetDirectoryName(Path.GetFullPath(skillFilePath)),
            json.RootElement.GetProperty("directoryPath").GetString());
        Assert.Contains("# Reviewer", json.RootElement.GetProperty("content").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Skill_ReturnsNotFoundWithAvailableSkillNames()
    {
        WriteSkill(
            "planner",
            """
            ---
            name: planner
            description: Plan multi-step implementation work.
            ---
            Plan carefully.
            """);
        var tools = CreateTools();

        var result = await tools.skill("missing");

        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("skill_not_found", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("missing", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            ["planner"],
            json.RootElement.GetProperty("availableSkills").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
    }

    [Fact]
    public void SkillPromptAugmenter_AppendsSkillMetadataOnlyWhenEnabled()
    {
        WriteSkill(
            "debugger",
            """
            ---
            name: debugger
            description: Investigate runtime failures systematically.
            ---
            This body should not be present in the prompt preview.
            """);
        var catalog = new SkillCatalog(Path.Combine(_tempRoot, "skills"), new TextFileReader());
        var augmenter = new SkillPromptAugmenter(catalog);

        var enabledPrompt = augmenter.AppendAvailableSkillsIfEnabled(
            "Base prompt.",
            new HashSet<string>([SkillTools.BaseToolName], StringComparer.OrdinalIgnoreCase));
        var disabledPrompt = augmenter.AppendAvailableSkillsIfEnabled(
            "Base prompt.",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Contains("Base prompt.", enabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Available skills via the `skill` tool:", enabledPrompt, StringComparison.Ordinal);
        Assert.Contains("debugger: Investigate runtime failures systematically.", enabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("This body should not be present", enabledPrompt, StringComparison.Ordinal);
        Assert.Equal("Base prompt.", disabledPrompt);
    }

    [Fact]
    public void SkillCatalog_RejectsUnsupportedFrontMatterKeys()
    {
        WriteSkill(
            "invalid-skill",
            """
            ---
            name: invalid
            description: Invalid skill
            when_to_use: never
            ---
            body
            """);
        var catalog = new SkillCatalog(Path.Combine(_tempRoot, "skills"), new TextFileReader());

        var ex = Assert.Throws<InvalidDataException>(() => catalog.GetAvailableSkills());
        Assert.Contains("name, description 以外は未対応", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Skill_ExposesExpectedFunctionName()
    {
        var tools = CreateTools();

        Assert.Equal(SkillTools.BaseToolName, tools.skillFunctionContract.Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private SkillTools CreateTools() =>
        new(new SkillCatalog(Path.Combine(_tempRoot, "skills"), new TextFileReader()));

    private string WriteSkill(string directoryName, string markdown)
    {
        var directoryPath = Directory.CreateDirectory(Path.Combine(_tempRoot, "skills", directoryName)).FullName;
        var filePath = Path.Combine(directoryPath, "SKILL.md");
        File.WriteAllText(filePath, markdown, TextEncodingDefaults.Utf8NoBom);
        return filePath;
    }
}
