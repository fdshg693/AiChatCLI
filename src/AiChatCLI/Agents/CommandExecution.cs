using System.Diagnostics;
using System.Text;

namespace AiChatCLI;

internal interface ICommandExecutor
{
    Task<CommandExecutionResult> ExecuteAsync(string command, TimeSpan timeout);
}

internal sealed record CommandExecutionResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut);

internal sealed class LocalCommandExecutor : ICommandExecutor
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<CommandExecutionResult> ExecuteAsync(string command, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(command)
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) => AppendLine(stdout, args.Data);
        process.ErrorDataReceived += (_, args) => AppendLine(stderr, args.Data);

        if (!process.Start())
            throw new InvalidOperationException("コマンドプロセスを開始できませんでした。");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
            return new CommandExecutionResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await WaitAfterKillAsync(process);
            return new CommandExecutionResult(-1, stdout.ToString(), stderr.ToString(), true);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "powershell.exe";
            startInfo.StandardOutputEncoding = Utf8NoBom;
            startInfo.StandardErrorEncoding = Utf8NoBom;
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(WrapWindowsCommand(command));
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add(command);
        }

        return startInfo;
    }

    private static string WrapWindowsCommand(string command) =>
        """
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [Console]::InputEncoding = $utf8NoBom
        [Console]::OutputEncoding = $utf8NoBom
        $OutputEncoding = $utf8NoBom
        chcp 65001 > $null
        """ + Environment.NewLine + command;

    private static void AppendLine(StringBuilder builder, string? line)
    {
        if (line is null)
            return;

        builder.AppendLine(line);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task WaitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
