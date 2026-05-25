namespace AiChatCLI;

internal sealed class SkillCatalog
{
    private readonly TextFileReader _reader;

    public SkillCatalog(string rootDirectoryPath, TextFileReader reader)
    {
        if (string.IsNullOrWhiteSpace(rootDirectoryPath))
            throw new ArgumentException("rootDirectoryPath を指定してください。", nameof(rootDirectoryPath));

        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        RootDirectoryPath = Path.GetFullPath(rootDirectoryPath.Trim());
    }

    public string RootDirectoryPath { get; }

    public IReadOnlyList<SkillSummary> GetAvailableSkills() =>
        LoadSkills()
            .Select(skill => new SkillSummary(skill.Name, skill.Description))
            .ToArray();

    public bool TryGetSkill(string name, out SkillDefinition? skill)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            skill = null;
            return false;
        }

        var requestedName = name.Trim();
        skill = LoadSkills().FirstOrDefault(candidate =>
            string.Equals(candidate.Name, requestedName, StringComparison.OrdinalIgnoreCase));
        return skill is not null;
    }

    private IReadOnlyList<SkillDefinition> LoadSkills()
    {
        if (!Directory.Exists(RootDirectoryPath))
            return [];

        var skills = new List<SkillDefinition>();
        var seenNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory
                     .EnumerateFiles(RootDirectoryPath, "SKILL.md", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var skill = ParseSkillFile(filePath);
            if (seenNames.TryGetValue(skill.Name, out var existingPath))
            {
                throw new InvalidDataException(
                    $"同名の skill が複数あります: '{skill.Name}' ({existingPath}, {skill.FilePath})");
            }

            seenNames[skill.Name] = skill.FilePath;
            skills.Add(skill);
        }

        return skills
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal SkillDefinition ParseSkillFile(string filePath)
    {
        var resolvedPath = Path.GetFullPath(filePath);
        var markdown = _reader.Read(resolvedPath).Content;
        var normalized = NormalizeLineEndings(markdown);
        var lines = normalized.Split('\n', StringSplitOptions.None);
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"skill の front matter が見つかりません: {resolvedPath}");
        }

        var frontMatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        for (; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidDataException(
                    $"skill の front matter 行が不正です: {resolvedPath} ({line})");
            }

            var key = line[..separatorIndex].Trim();
            if (!IsSupportedFrontMatterKey(key))
            {
                throw new InvalidDataException(
                    $"skill の front matter では name, description 以外は未対応です: {resolvedPath} ({key})");
            }

            if (frontMatter.ContainsKey(key))
            {
                throw new InvalidDataException(
                    $"skill の front matter に重複キーがあります: {resolvedPath} ({key})");
            }

            frontMatter[key] = Unquote(line[(separatorIndex + 1)..].Trim());
        }

        if (index >= lines.Length || !string.Equals(lines[index].Trim(), "---", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"skill の front matter 終了区切りが見つかりません: {resolvedPath}");
        }

        if (!frontMatter.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"skill の name は必須です: {resolvedPath}");

        if (!frontMatter.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(description))
            throw new InvalidDataException($"skill の description は必須です: {resolvedPath}");

        var content = string.Join('\n', lines.Skip(index + 1));
        return new SkillDefinition(name.Trim(), description.Trim(), resolvedPath, content);
    }

    private static bool IsSupportedFrontMatterKey(string key) =>
        string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "description", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
             (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
        {
            return value[1..^1];
        }

        return value;
    }
}

internal sealed record SkillSummary(string Name, string Description);

internal sealed record SkillDefinition(
    string Name,
    string Description,
    string FilePath,
    string Content);
