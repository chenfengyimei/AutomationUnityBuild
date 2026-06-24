using System.Text.Json;

namespace BuildServer.Reverse;

public sealed class AgentCredential
{
    public string NodeId { get; set; } = "";
    public string Credential { get; set; } = "";
    public string GatewayUrl { get; set; } = "";
    public DateTimeOffset EnrolledAt { get; set; } = DateTimeOffset.Now;
    public bool AutoConnect { get; set; } = true;
}

public sealed class AgentCredentialStore(BuildServerOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string DefaultCredentialPath => string.IsNullOrWhiteSpace(options.ReverseCredentialPath)
        ? Path.Combine(options.DataRoot, "reverse-agent-credential.json")
        : options.ReverseCredentialPath;

    public async Task<AgentCredential?> LoadAsync()
    {
        string path = DefaultCredentialPath;
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path);
        AgentCredential? credential = JsonSerializer.Deserialize<AgentCredential>(json, JsonOptions);
        return credential;
    }

    public async Task SaveAsync(AgentCredential credential)
    {
        string path = DefaultCredentialPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(credential, JsonOptions);
        await File.WriteAllTextAsync(path, json);

        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
        }
    }

    public void Delete()
    {
        string path = DefaultCredentialPath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task UpdateAutoConnectAsync(bool autoConnect)
    {
        AgentCredential? cred = await LoadAsync();
        if (cred is null) return;
        cred.AutoConnect = autoConnect;
        await SaveAsync(cred);
    }
}
