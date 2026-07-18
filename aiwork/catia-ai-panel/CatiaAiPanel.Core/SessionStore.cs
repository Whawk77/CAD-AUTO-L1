using System.Text.Json;

namespace CatiaAiPanel.Core;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public SessionStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatiaAiPanel", "sessions");
    }

    public string RootDirectory { get; }

    public ConversationState Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(RootDirectory, id);
        Directory.CreateDirectory(directory);
        return new() { LocalSessionId = id, SessionDirectory = directory };
    }

    public ConversationState? LoadLatestPending()
    {
        if (!Directory.Exists(RootDirectory)) return null;
        foreach (var path in Directory.EnumerateFiles(RootDirectory, "state.json", SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var state = JsonSerializer.Deserialize<ConversationState>(File.ReadAllText(path), Options);
                if (state?.CurrentProposal is { HasExecutableCode: true } &&
                    (state.LastExecution is null || (!state.LastExecution.Success && state.CorrectionCount > 0)))
                    return state;
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    public Task SaveAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(state.SessionDirectory);
        return File.WriteAllTextAsync(
            Path.Combine(state.SessionDirectory, "state.json"),
            JsonSerializer.Serialize(state, Options),
            cancellationToken);
    }
}
