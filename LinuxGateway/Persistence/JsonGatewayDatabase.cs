using System.Text.Json;

namespace LinuxGateway.Persistence;

public sealed class JsonGatewayDatabase(LinuxGatewayOptions options)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string DatabasePath => Path.Combine(options.DataRoot, "linux-gateway-db.json");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(options.DataRoot);
        if (!File.Exists(DatabasePath))
        {
            await SaveUnlockedAsync(new GatewayDatabase());
            TryRestrictSecretFile(DatabasePath);
        }
    }

    public async Task<T> ReadAsync<T>(Func<GatewayDatabase, T> read)
    {
        await _lock.WaitAsync();
        try
        {
            GatewayDatabase database = await LoadUnlockedAsync();
            return read(database);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> UpdateAsync<T>(Func<GatewayDatabase, T> update)
    {
        await _lock.WaitAsync();
        try
        {
            GatewayDatabase database = await LoadUnlockedAsync();
            T result = update(database);
            await SaveUnlockedAsync(database);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Action<GatewayDatabase> update)
    {
        await UpdateAsync(database =>
        {
            update(database);
            return true;
        });
    }

    private async Task<GatewayDatabase> LoadUnlockedAsync()
    {
        if (!File.Exists(DatabasePath))
        {
            return new GatewayDatabase();
        }

        await using FileStream stream = File.OpenRead(DatabasePath);
        return await JsonSerializer.DeserializeAsync<GatewayDatabase>(stream, _jsonOptions) ?? new GatewayDatabase();
    }

    private async Task SaveUnlockedAsync(GatewayDatabase database)
    {
        Directory.CreateDirectory(options.DataRoot);
        string tempPath = $"{DatabasePath}.{Guid.NewGuid():N}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, database, _jsonOptions);
            await stream.FlushAsync();
        }

        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }

        File.Move(tempPath, DatabasePath);
        TryRestrictSecretFile(DatabasePath);
    }

    private static void TryRestrictSecretFile(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // 权限收紧失败不阻止服务启动，部署文档会提醒保护数据目录。
        }
    }
}
