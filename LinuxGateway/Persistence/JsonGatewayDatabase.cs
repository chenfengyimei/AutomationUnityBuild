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
    private string LockPath => Path.Combine(options.DataRoot, "linux-gateway-db.lock");

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(options.DataRoot);
            await using FileStream _ = await AcquireProcessLockAsync();
            if (!File.Exists(DatabasePath))
            {
                await SaveUnlockedAsync(new GatewayDatabase());
                TryRestrictSecretFile(DatabasePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> ReadAsync<T>(Func<GatewayDatabase, T> read)
    {
        await _lock.WaitAsync();
        try
        {
            await using FileStream _ = await AcquireProcessLockAsync();
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
            await using FileStream _ = await AcquireProcessLockAsync();
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
        string tempPath = $"{DatabasePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, database, _jsonOptions);
                await stream.FlushAsync();
            }

            await MoveWithRetryAsync(tempPath, DatabasePath);
            TryRestrictSecretFile(DatabasePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"保存 LinuxGateway 数据库失败: {DatabasePath}{Environment.NewLine}" +
                "请确认没有启动多个 LinuxGateway，数据目录有写权限，并且 linux-gateway-db.json 没有被编辑器或同步软件锁定。",
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
                Directory.CreateDirectory(options.DataRoot);
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        throw new IOException(
            $"等待 LinuxGateway 数据库锁超时: {LockPath}{Environment.NewLine}" +
            "通常是另一个 LinuxGateway 进程正在使用同一个数据目录。请关闭重复进程，或为不同实例指定不同的 LINUX_GATEWAY_DATA_ROOT。",
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
        }
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
