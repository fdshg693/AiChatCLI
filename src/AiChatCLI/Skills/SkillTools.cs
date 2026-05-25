using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AutoGen.Core;

namespace AiChatCLI;

/// <summary>
/// Provides an AI-callable tool that loads a skill markdown body by skill name.
/// </summary>
public partial class SkillTools
{
    /// <summary>
    /// Tool name used in <c>agents.json</c> agent tool lists.
    /// </summary>
    public const string BaseToolName = "skill";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly SkillCatalog _skillCatalog;

    internal SkillTools(SkillCatalog skillCatalog)
    {
        _skillCatalog = skillCatalog;
    }

    /// <summary>
    /// Loads a local skill by its front-matter <c>name</c>.
    /// The model receives only skill names and descriptions in the system prompt;
    /// call this tool only when one of those skills is relevant to the current task.
    /// </summary>
    /// <param name="name">The skill name declared in the skill front matter.</param>
    /// <returns>A JSON string containing the skill metadata, absolute SKILL.md path, containing directory path, markdown content, and error state.</returns>
    [Function]
    public Task<string> skill(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(Serialize(SkillToolResponse.Invalid("name を指定してください。")));

        if (!_skillCatalog.TryGetSkill(name, out var skill))
        {
            return Task.FromResult(Serialize(SkillToolResponse.NotFound(
                name.Trim(),
                _skillCatalog.GetAvailableSkills().Select(entry => entry.Name).ToArray())));
        }

        return Task.FromResult(Serialize(SkillToolResponse.Success(
            skill!.Name,
            skill.Description,
            skill.FilePath,
            Path.GetDirectoryName(skill.FilePath) ?? string.Empty,
            skill.Content)));
    }

    private static string Serialize(SkillToolResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}

internal sealed record SkillToolResponse(
    bool Ok,
    string Status,
    string? Name,
    string? Description,
    string? Path,
    string? DirectoryPath,
    string? Content,
    IReadOnlyList<string>? AvailableSkills,
    string? Error)
{
    public static SkillToolResponse Success(
        string name,
        string description,
        string path,
        string directoryPath,
        string content) =>
        new(true, "completed", name, description, path, directoryPath, content, null, null);

    public static SkillToolResponse Invalid(string error) =>
        new(false, "invalid_request", null, null, null, null, null, null, error);

    public static SkillToolResponse NotFound(string name, IReadOnlyList<string> availableSkills) =>
        new(
            false,
            "skill_not_found",
            name,
            null,
            null,
            null,
            null,
            availableSkills,
            "指定された skill は見つかりませんでした。");
}
