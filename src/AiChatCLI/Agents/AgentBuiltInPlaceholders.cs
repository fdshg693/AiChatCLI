using System.Runtime.InteropServices;

namespace AiChatCLI;

internal static class AgentBuiltInPlaceholders
{
    public const string SystemInfoKey = "SYSTEM_INFO";

    public static IReadOnlyDictionary<string, string> Create()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SystemInfoKey] = BuildSystemInfo()
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
            $"- Command shell: {GetCommandShellDescription()}",
            $"- Command encoding: {GetCommandEncodingDescription()}"
        });
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
