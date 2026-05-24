using System.Runtime.InteropServices;

namespace AiChatCLI;

internal static class AgentBuiltInPlaceholders
{
    public const string SystemInfoKey = "SYSTEM_INFO";
    public const string CurrentDirectoryEntriesKey = "CURRENT_DIRECTORY_ENTRIES";

    public static IReadOnlyDictionary<string, string> Create()
    {
        var resolvePlaceholder = CreateResolver();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SystemInfoKey] = resolvePlaceholder(SystemInfoKey) ?? string.Empty,
            [CurrentDirectoryEntriesKey] = resolvePlaceholder(CurrentDirectoryEntriesKey) ?? string.Empty
        };
    }

    public static Func<string, string?> CreateResolver(SessionWorkingDirectory? workingDirectory = null)
    {
        var currentDirectory = workingDirectory?.CurrentDirectory ?? Directory.GetCurrentDirectory();
        return key => key switch
        {
            SystemInfoKey => BuildSystemInfo(),
            CurrentDirectoryEntriesKey => BuildCurrentDirectoryEntries(currentDirectory),
            _ => null
        };
    }

    private static string BuildSystemInfo()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Local runtime information:",
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- OS architecture: {RuntimeInformation.OSArchitecture}",
            $"- Process architecture: {RuntimeInformation.ProcessArchitecture}",
            $"- File read tool: relative paths are resolved from the CLI process current directory; it detects BOMs, prefers UTF-8, and falls back to Windows code pages when needed.",
            $"- Command shell: {GetCommandShellDescription()}",
            $"- Command encoding: {GetCommandEncodingDescription()}"
        });
    }

    private static string BuildCurrentDirectoryEntries(string currentDirectory)
    {
        var lines = new List<string>
        {
            "Session current directory contents:",
            $"- Current directory: {currentDirectory}"
        };

        try
        {
            var entries = new DirectoryInfo(currentDirectory)
                .EnumerateFileSystemInfos()
                .Select(entry => new
                {
                    entry.Name,
                    IsDirectory = entry is DirectoryInfo
                })
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"  - [{(entry.IsDirectory ? "dir" : "file")}] {entry.Name}")
                .ToArray();

            if (entries.Length == 0)
            {
                lines.Add("- Direct children: (empty)");
            }
            else
            {
                lines.Add("- Direct children:");
                lines.AddRange(entries);
            }
        }
        catch (IOException)
        {
            lines.Add("- Direct children: unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            lines.Add("- Direct children: unavailable.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetCommandShellDescription()
    {
        return OperatingSystem.IsWindows()
            ? "PowerShell via powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command"
            : "/bin/sh -lc";
    }

    private static string GetCommandEncodingDescription()
    {
        return OperatingSystem.IsWindows()
            ? "PowerShell stdin/stdout/stderr are configured as UTF-8 before running commands."
            : "The command tool uses the process default locale and encoding.";
    }
}
