using System.Diagnostics;
using System.ComponentModel;
using OpenClawManager.Core.Models;

namespace OpenClawManager.Infrastructure;

public sealed class AdminElevation
{
    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var started = DateTimeOffset.UtcNow;
        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return new CommandResult(-1, string.Empty, "无法启动提权进程。", DateTimeOffset.UtcNow - started, false, false);
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                }
                catch { }

                return new CommandResult(
                    -1,
                    string.Empty,
                    "提权操作超时或被取消。",
                    DateTimeOffset.UtcNow - started,
                    !cancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested);
            }

            return new CommandResult(
                process.ExitCode,
                string.Empty,
                string.Empty,
                DateTimeOffset.UtcNow - started,
                false,
                false);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new CommandResult(-1, string.Empty, "用户取消了管理员权限请求。", DateTimeOffset.UtcNow - started, false, true);
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, ex.Message, DateTimeOffset.UtcNow - started, false, false);
        }
    }
}
