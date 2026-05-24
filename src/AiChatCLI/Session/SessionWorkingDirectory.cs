namespace AiChatCLI;

internal sealed class SessionWorkingDirectory
{
    private string _currentDirectory;

    public SessionWorkingDirectory(string? initialDirectory = null)
    {
        var effectiveDirectory = string.IsNullOrWhiteSpace(initialDirectory)
            ? Directory.GetCurrentDirectory()
            : initialDirectory.Trim();

        _currentDirectory = Path.GetFullPath(effectiveDirectory);
    }

    public string CurrentDirectory => _currentDirectory;

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path を指定してください。", nameof(path));

        var trimmed = path.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(_currentDirectory, trimmed));
    }
}
