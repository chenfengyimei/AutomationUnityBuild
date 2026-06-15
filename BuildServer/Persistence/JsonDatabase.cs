using System.Text.Json;

namespace BuildServer.Persistence;

public sealed class JsonDatabase
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JsonDatabase(BuildServerOptions options)
    {
        Directory.CreateDirectory(options.DataRoot);
        _path = Path.Combine(options.DataRoot, "buildserver-db.json");
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_path))
            {
                await SaveUnlockedAsync(new BuildServerDatabase());
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> ReadAsync<T>(Func<BuildServerDatabase, T> read)
    {
        await _lock.WaitAsync();
        try
        {
            BuildServerDatabase database = await LoadUnlockedAsync();
            return read(database);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> UpdateAsync<T>(Func<BuildServerDatabase, T> update)
    {
        await _lock.WaitAsync();
        try
        {
            BuildServerDatabase database = await LoadUnlockedAsync();
            T result = update(database);
            await SaveUnlockedAsync(database);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Action<BuildServerDatabase> update)
    {
        await UpdateAsync(database =>
        {
            update(database);
            return true;
        });
    }

    private async Task<BuildServerDatabase> LoadUnlockedAsync()
    {
        if (!File.Exists(_path))
        {
            return new BuildServerDatabase();
        }

        string json = await File.ReadAllTextAsync(_path);
        return JsonSerializer.Deserialize<BuildServerDatabase>(json, _jsonOptions) ?? new BuildServerDatabase();
    }

    private async Task SaveUnlockedAsync(BuildServerDatabase database)
    {
        string tempPath = _path + ".tmp";
        string json = JsonSerializer.Serialize(database, _jsonOptions);
        await File.WriteAllTextAsync(tempPath, json + Environment.NewLine);
        File.Move(tempPath, _path, overwrite: true);
    }
}
