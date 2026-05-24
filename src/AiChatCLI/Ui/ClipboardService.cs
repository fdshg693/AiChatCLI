using System.Diagnostics;
using System.Text;

namespace AiChatCLI;

internal interface IClipboardService
{
    bool TryGetText(out string? text);
}

internal sealed class SystemClipboardService : IClipboardService
{
    public bool TryGetText(out string? text)
    {
        foreach (var command in GetClipboardReadCommands())
        {
            if (TryReadClipboard(command.FileName, command.Arguments, out text))
                return true;
        }

        text = null;
        return false;
    }

    private static IEnumerable<(string FileName, string[] Arguments)> GetClipboardReadCommands()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return ("powershell", ["-NoProfile", "-STA", "-Command", "Get-Clipboard -Raw"]);
            yield return ("pwsh", ["-NoProfile", "-Command", "Get-Clipboard -Raw"]);
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return ("pbpaste", []);
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return ("wl-paste", ["--no-newline"]);
            yield return ("xclip", ["-selection", "clipboard", "-o"]);
            yield return ("xsel", ["--clipboard", "--output"]);
        }
    }

    private static bool TryReadClipboard(string fileName, IReadOnlyList<string> arguments, out string? text)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                text = null;
                return false;
            }

            text = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            text = null;
            return false;
        }
    }
}
