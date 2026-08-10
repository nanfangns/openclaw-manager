using System.Text.Json;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class OpenClawCliService : IOpenClawCliService
{
    private readonly IProcessRunner _runner;
    private readonly ILogService _logs;

    public OpenClawCliService(IProcessRunner runner, ILogService logs)
    {
        _runner = runner;
        _logs = logs;
    }

    public async Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken)
    {
        PathEnvironment.RefreshProcessPath();
        var pathResult = await _runner.RunAsync(
            "where.exe",
            new[] { CommandCatalog.OpenClaw },
            new ProcessRunOptions(TimeSpan.FromSeconds(10)),
            cancellationToken);
        var path = pathResult.IsSuccess
            ? pathResult.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;

        var result = await _runner.RunAsync(
            path ?? CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawVersion(),
            new ProcessRunOptions(TimeSpan.FromSeconds(20)),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return new OpenClawVersionResult(false, null, path, "OpenClaw CLI 不可用");
        }

        return new OpenClawVersionResult(true, result.StandardOutput.Trim(), path, "OpenClaw 已安装");
    }

    public async Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken)
    {
        _logs.Write(AppLogLevel.Information, $"正在安装 OpenClaw {packageSpec}");
        var result = await _runner.RunAsync(
            CommandCatalog.Npm,
            CommandCatalog.NpmInstallGlobal(packageSpec),
            new ProcessRunOptions(TimeSpan.FromMinutes(10)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logs.Write(AppLogLevel.Error, "OpenClaw npm 安装失败", new Dictionary<string, string>
            {
                ["exitCode"] = result.ExitCode.ToString(),
                ["stderr"] = result.StandardError
            });
        }

        return result;
    }

    public Task<CommandResult> UninstallAsync(CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.Npm,
            CommandCatalog.NpmUninstallGlobal("openclaw"),
            new ProcessRunOptions(TimeSpan.FromMinutes(5)),
            cancellationToken);

    public Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken)
        => _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawConfigValidate(),
            new ProcessRunOptions(TimeSpan.FromSeconds(60)),
            cancellationToken);

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawModelsList(),
            new ProcessRunOptions(TimeSpan.FromSeconds(60)),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return Array.Empty<string>();
        }

        return result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    public async Task<ModelProbeResult> ProbeModelAsync(
        ModelConfiguration? configuration,
        CancellationToken cancellationToken)
    {
        PathEnvironment.RefreshProcessPath();
        var environment = new Dictionary<string, string?>();
        if (configuration is not null)
        {
            var provider = ModelProviderCatalog.Find(configuration.ProviderId);
            if (provider?.EnvironmentVariable is not null && !string.IsNullOrWhiteSpace(configuration.ApiKey))
            {
                environment[provider.EnvironmentVariable] = configuration.ApiKey;
            }
        }

        var result = await _runner.RunAsync(
            CommandCatalog.OpenClaw,
            CommandCatalog.OpenClawModelsStatusJsonProbe(),
            new ProcessRunOptions(TimeSpan.FromMinutes(2), Environment: environment),
            cancellationToken);
        var output = string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput, result.StandardError }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            .Trim();
        var safeOutput = _logs.Redact(output);
        var model = configuration is null
            ? TryReadModel(output)
            : string.IsNullOrWhiteSpace(configuration.ModelId)
                ? configuration.ProviderId
                : configuration.ModelId.Contains('/', StringComparison.Ordinal)
                    ? configuration.ModelId
                    : $"{configuration.ProviderId}/{configuration.ModelId}";
        var configured = configuration is not null || !ContainsNoModel(output);

        if (!result.IsSuccess)
        {
            var reason = FindFailureReason(output);
            return new ModelProbeResult(
                false,
                configured,
                model,
                string.IsNullOrWhiteSpace(reason) ? "模型探测失败" : $"模型探测失败：{reason}",
                safeOutput);
        }

        if (!configured)
        {
            return new ModelProbeResult(true, false, null, "未发现已配置模型", safeOutput);
        }

        if (ContainsProbeFailure(output))
        {
            return new ModelProbeResult(false, true, model, $"模型探测失败：{FindFailureReason(output) ?? "服务商或模型不可用"}", safeOutput);
        }

        return new ModelProbeResult(true, true, model, "模型探测成功", safeOutput);
    }

    private static string? TryReadModel(string output)
    {
        var json = ExtractJsonObject(output);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return FindString(document.RootElement, "model", "modelId", "defaultModel", "resolvedModel");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ContainsNoModel(string output)
        => output.Contains("no_model", StringComparison.OrdinalIgnoreCase)
            || output.Contains("no model", StringComparison.OrdinalIgnoreCase)
            || output.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            || output.Contains("未配置", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsProbeFailure(string output)
    {
        var json = ExtractJsonObject(output);
        if (json is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var property in EnumerateProperties(document.RootElement))
                {
                    var name = property.Name;
                    if (name.Equals("status", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("state", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String && IsFailureStatus(property.Value.GetString()))
                        {
                            return true;
                        }
                    }
                    else if ((name.Equals("error", StringComparison.OrdinalIgnoreCase)
                              || name.Equals("reason", StringComparison.OrdinalIgnoreCase))
                             && property.Value.ValueKind == JsonValueKind.String
                             && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    {
                        return true;
                    }
                    else if ((name.Equals("ok", StringComparison.OrdinalIgnoreCase)
                              || name.Equals("healthy", StringComparison.OrdinalIgnoreCase)
                              || name.Equals("connected", StringComparison.OrdinalIgnoreCase)
                              || name.Equals("success", StringComparison.OrdinalIgnoreCase))
                             && property.Value.ValueKind == JsonValueKind.False)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (JsonException)
            {
                // Fall back to text matching for CLI versions that prefix malformed JSON.
            }
        }

        return ContainsProbeFailureText(output);
    }

    private static bool ContainsProbeFailureText(string output)
        => output.Contains("unknown model", StringComparison.OrdinalIgnoreCase)
            || output.Contains("invalid model", StringComparison.OrdinalIgnoreCase)
            || output.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || output.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || output.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || output.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("error", StringComparison.OrdinalIgnoreCase)
            || output.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || output.Contains("错误", StringComparison.OrdinalIgnoreCase)
            || output.Contains("失败", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailureStatus(string? status)
        => status is not null && (status.Equals("error", StringComparison.OrdinalIgnoreCase)
            || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || status.Equals("timeout", StringComparison.OrdinalIgnoreCase)
            || status.Equals("unavailable", StringComparison.OrdinalIgnoreCase)
            || status.Equals("missing", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<JsonProperty> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property;
                foreach (var nested in EnumerateProperties(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateProperties(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string? FindFailureReason(string output)
    {
        var candidates = new[] { "unknown model", "invalid model", "unauthorized", "authentication failed", "timeout", "not configured", "error", "failed" };
        return candidates.FirstOrDefault(candidate => output.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var direct) && direct.ValueKind == JsonValueKind.String)
            {
                return direct.GetString();
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            var nested = property.Value.ValueKind == JsonValueKind.Object
                ? FindString(property.Value, names)
                : null;
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string? ExtractJsonObject(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        return start >= 0 && end > start ? output[start..(end + 1)] : null;
    }
}
