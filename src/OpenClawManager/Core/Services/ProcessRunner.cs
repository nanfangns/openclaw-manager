using System.Diagnostics;
using System.Text;
using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ProcessRunOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = options.WorkingDirectory
                ?? Environment.CurrentDirectory
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (options.Environment is not null)
        {
            foreach (var item in options.Environment)
            {
                startInfo.Environment[item.Key] = item.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                return new CommandResult(-1, string.Empty, "Process did not start.", stopwatch.Elapsed, false, false);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                var cancelled = cancellationToken.IsCancellationRequested;
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                return new CommandResult(
                    -1,
                    stdout.ToString(),
                    stderr.ToString(),
                    stopwatch.Elapsed,
                    !cancelled,
                    cancelled);
            }

            process.WaitForExit();
            return new CommandResult(
                process.ExitCode,
                stdout.ToString(),
                stderr.ToString(),
                stopwatch.Elapsed,
                false,
                false);
        }
        catch (Exception ex)
        {
            TryKill(process);
            return new CommandResult(-1, stdout.ToString(), $"{stderr}{ex.Message}", stopwatch.Elapsed, false, false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The original process result is more useful than a cleanup exception.
        }
    }
}
