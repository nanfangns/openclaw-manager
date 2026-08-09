using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClawManager.Core.Models;
using OpenClawManager.Infrastructure;

namespace OpenClawManager.Core.Services;

public sealed class JsonStateStore : IStateStore
{
    private readonly PathLayout _paths;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStateStore(PathLayout paths)
    {
        _paths = paths;
    }

    public async Task<InstallState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.StateFile))
        {
            return InstallState.Empty;
        }

        try
        {
            await using var stream = File.OpenRead(_paths.StateFile);
            return await JsonSerializer.DeserializeAsync<InstallState>(stream, _options, cancellationToken)
                ?? InstallState.Empty;
        }
        catch (JsonException)
        {
            return InstallState.Empty;
        }
        catch (IOException)
        {
            return InstallState.Empty;
        }
    }

    public async Task SaveAsync(InstallState state, CancellationToken cancellationToken)
    {
        _paths.EnsureDataDirectories();
        var tempPath = _paths.StateFile + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, _options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, _paths.StateFile, true);
    }
}
