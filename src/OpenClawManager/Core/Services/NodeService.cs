using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class NodeService : INodeService
{
    private const string ReleaseIndexUrl = "https://nodejs.org/dist/index.json";
    private readonly IEnvironmentService _environment;
    private readonly IProcessRunner _runner;
    private readonly ILogService _logs;
    private readonly AdminElevation _elevation;
    private readonly HttpClient _httpClient;

    public NodeService(
        IEnvironmentService environment,
        IProcessRunner runner,
        ILogService logs,
        AdminElevation elevation,
        HttpClient? httpClient = null)
    {
        _environment = environment;
        _runner = runner;
        _logs = logs;
        _elevation = elevation;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<NodeResult> EnsureCompatibleAsync(
        NodeInstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new InstallProgress(InstallStep.VerifyingNode, 12, "正在检查 Node.js 环境"));
        var current = await _environment.DetectAsync(cancellationToken);
        if (VersionPolicy.IsNodeSupported(current.NodeVersion))
        {
            return new NodeResult(true, current.NodeVersion, current.NodePath, false, "已发现兼容的 Node.js");
        }

        if (!options.AllowInstall)
        {
            return new NodeResult(false, current.NodeVersion, current.NodePath, false, "未找到兼容的 Node.js");
        }

        progress.Report(new InstallProgress(InstallStep.InstallingNode, 20, "正在获取兼容的 Node.js 版本"));
        var release = await SelectReleaseAsync(cancellationToken);
        var msiPath = Path.Combine(Path.GetTempPath(), $"openclaw-node-{Guid.NewGuid():N}.msi");

        try
        {
            progress.Report(new InstallProgress(InstallStep.InstallingNode, 28, $"正在下载 Node.js {release.Version}"));
            await DownloadAndVerifyAsync(release, msiPath, cancellationToken);

            progress.Report(new InstallProgress(InstallStep.InstallingNode, 38, "正在安装 Node.js"));
            var installResult = await _elevation.RunAsync(
                "msiexec.exe",
                new[] { "/i", msiPath, "/qn", "/norestart" },
                TimeSpan.FromMinutes(5),
                cancellationToken);
            if (!installResult.IsSuccess)
            {
                _logs.Write(AppLogLevel.Error, "Node.js 安装失败", new Dictionary<string, string>
                {
                    ["exitCode"] = installResult.ExitCode.ToString(),
                    ["stderr"] = installResult.StandardError
                });
                return new NodeResult(false, null, null, false, "Node.js 安装失败");
            }

            progress.Report(new InstallProgress(InstallStep.VerifyingNode, 45, "正在验证 Node.js"));
            var after = await _environment.DetectAsync(cancellationToken);
            if (!VersionPolicy.IsNodeSupported(after.NodeVersion))
            {
                return new NodeResult(false, after.NodeVersion, after.NodePath, false, "Node.js 安装后版本仍不兼容");
            }

            return new NodeResult(true, after.NodeVersion, after.NodePath, true, "Node.js 安装成功");
        }
        finally
        {
            try { File.Delete(msiPath); } catch { }
        }
    }

    private async Task<NodeRelease> SelectReleaseAsync(CancellationToken cancellationToken)
    {
        await using var stream = await _httpClient.GetStreamAsync(ReleaseIndexUrl, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var versionText = item.GetProperty("version").GetString();
            if (!VersionPolicy.IsNodeSupported(versionText))
            {
                continue;
            }

            var version = versionText!.TrimStart('v');
            var msiFile = $"node-v{version}-x64.msi";
            var files = item.GetProperty("files").EnumerateArray().Select(x => x.GetString()).ToHashSet();
            if (files.Contains("win-x64-msi"))
            {
                return new NodeRelease(
                    versionText,
                    $"https://nodejs.org/dist/v{version}/{msiFile}",
                    $"https://nodejs.org/dist/v{version}/SHASUMS256.txt",
                    msiFile);
            }
        }

        throw new InvalidOperationException("Node.js 官方发行列表中没有找到兼容的 x64 MSI。");
    }

    private async Task DownloadAndVerifyAsync(
        NodeRelease release,
        string destination,
        CancellationToken cancellationToken)
    {
        var bytes = await _httpClient.GetByteArrayAsync(release.DownloadUrl, cancellationToken);
        var sums = await _httpClient.GetStringAsync(release.ChecksumUrl, cancellationToken);
        var expected = sums.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[1].TrimStart('*') == release.FileName)
            .Select(parts => parts[0])
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidDataException($"Node.js 校验文件中没有 {release.FileName}。");
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Node.js 下载文件 SHA-256 校验失败。");
        }

        await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
    }

    private sealed record NodeRelease(
        string Version,
        string DownloadUrl,
        string ChecksumUrl,
        string FileName);
}
