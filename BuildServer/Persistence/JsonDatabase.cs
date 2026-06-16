using System.Text.Json;

namespace BuildServer.Persistence;

public sealed class JsonDatabase
{
    private readonly string _path;
    private readonly string _lockPath;
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
        _lockPath = Path.Combine(options.DataRoot, "buildserver-db.lock");
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using FileStream _ = await AcquireProcessLockAsync();
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
            await using FileStream _ = await AcquireProcessLockAsync();
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
            await using FileStream _ = await AcquireProcessLockAsync();
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
        string tempPath = $"{_path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        string json = JsonSerializer.Serialize(database, _jsonOptions);
        try
        {
            await File.WriteAllTextAsync(tempPath, json + Environment.NewLine);
            await MoveWithRetryAsync(tempPath, _path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"保存 BuildServer 数据库失败: {_path}{Environment.NewLine}" +
                "请确认没有启动多个 BuildServer、数据目录有写权限，并且 buildserver-db.json 没有被编辑器/杀毒软件/同步软件锁定。",
                ex);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private async Task<FileStream> AcquireProcessLockAsync()
    {
        Exception? lastException = null;
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        throw new IOException(
            $"等待 BuildServer 数据库锁超时: {_lockPath}{Environment.NewLine}" +
            "通常是另一个 BuildServer 进程正在使用同一个数据目录。请关闭重复进程，或为不同实例指定不同的 BUILD_SERVER_DATA_ROOT。",
            lastException);
    }

    private static async Task MoveWithRetryAsync(string sourcePath, string targetPath)
    {
        Exception? lastException = null;
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                File.Move(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        throw lastException ?? new IOException($"移动文件失败: {sourcePath} -> {targetPath}");
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // 临时文件清理失败不影响主流程；下一次保存会使用唯一临时文件名。
        }
    }
}
