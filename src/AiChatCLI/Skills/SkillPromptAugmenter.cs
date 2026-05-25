namespace AiChatCLI;

internal sealed class SkillPromptAugmenter
{
    private readonly SkillCatalog _skillCatalog;

    public SkillPromptAugmenter(SkillCatalog skillCatalog)
    {
        _skillCatalog = skillCatalog;
    }

    public string AppendAvailableSkillsIfEnabled(string systemPrompt, IReadOnlySet<string> enabledTools)
    {
        if (!enabledTools.Contains(SkillTools.BaseToolName))
            return systemPrompt;

        var skillSection = FormatAvailableSkillsSection(_skillCatalog.GetAvailableSkills());
        if (string.IsNullOrWhiteSpace(skillSection))
            return systemPrompt;

        return string.IsNullOrWhiteSpace(systemPrompt)
            ? skillSection
            : $"{systemPrompt}\n\n{skillSection}";
    }

    internal static string FormatAvailableSkillsSection(IReadOnlyList<SkillSummary> skills)
    {
        if (skills.Count == 0)
            return "Available skills via the `skill` tool:\n- No skills are currently installed.";

        var lines = new List<string>
        {
            "Available skills via the `skill` tool:"
        };

        lines.AddRange(skills.Select(skill => $"- {skill.Name}: {skill.Description}"));
        return string.Join('\n', lines);
    }
}
