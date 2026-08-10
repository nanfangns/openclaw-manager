using System.Text.Json;
using OpenClawManager.Core.Models;

namespace OpenClawManager.Core.Services;

public static class GatewayStatusParser
{
    public static bool TryParse(string output, out GatewayStatus status)
    {
        status = default!;
        var json = ExtractJsonObject(output);
        if (json is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var running = FindBoolean(root, "running", "isRunning")
                ?? ReadState(FindValue(root, "runtime", "service", "gateway"));
            var probe = ReadProbe(root);
            var healthy = FindBoolean(root, "healthy", "isHealthy")
                ?? probe
                ?? running;
            var port = FindInt(root, "port") ?? 18789;
            var host = FindString(root, "host", "bindHost", "address") ?? "127.0.0.1";
            var processId = FindInt(root, "pid", "processId", "process_id");

            var isRunning = running == true;
            var isHealthy = isRunning && healthy == true;
            status = new GatewayStatus(
                true,
                isRunning,
                isHealthy,
                port,
                isHealthy ? "Gateway 运行正常" : isRunning ? "Gateway 正在运行，但连接探测未通过" : "Gateway 已安装但未运行",
                output.Trim(),
                host,
                probe,
                processId);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool? ReadProbe(JsonElement root)
    {
        var probeNode = FindValue(root, "connectivity", "probe", "rpc", "health");
        var probe = ReadState(probeNode);
        return probe ?? FindBoolean(root, "connected", "isConnected", "ok");
    }

    private static bool? ReadState(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return StateToBoolean(value.GetString());
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var direct = FindString(value, "status", "state", "result", "message");
            var parsed = StateToBoolean(direct);
            if (parsed is not null)
            {
                return parsed;
            }

            return FindBoolean(value, "running", "healthy", "connected", "ok");
        }

        return null;
    }

    private static bool? StateToBoolean(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return state.Trim().ToLowerInvariant() switch
        {
            "running" or "active" or "online" or "ok" or "healthy" or "connected" or "success" or "ready" => true,
            "stopped" or "inactive" or "offline" or "failed" or "error" or "unhealthy" or "disconnected" or "timeout" => false,
            _ => null
        };
    }

    private static bool? FindBoolean(JsonElement root, params string[] names)
    {
        var value = FindValue(root, names);
        return value is { ValueKind: JsonValueKind.True or JsonValueKind.False }
            ? value.Value.GetBoolean()
            : null;
    }

    private static int? FindInt(JsonElement root, params string[] names)
    {
        var value = FindValue(root, names);
        if (value is { ValueKind: JsonValueKind.Number } && value.Value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value is { ValueKind: JsonValueKind.String } && int.TryParse(value.Value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static string? FindString(JsonElement root, params string[] names)
    {
        var value = FindValue(root, names);
        return value is { ValueKind: JsonValueKind.String } ? value.Value.GetString() : null;
    }

    private static JsonElement? FindValue(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var direct))
            {
                return direct;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nested = FindValue(property.Value, names);
                if (nested is not null)
                {
                    return nested;
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    var nested = FindValue(item, names);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
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
