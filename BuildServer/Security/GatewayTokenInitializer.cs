namespace BuildServer.Security;

public static class GatewayTokenInitializer
{
    private const string InitialGatewayTokenFileName = "initial-gateway-token.txt";

    public static void Ensure(BuildServerOptions options, ILogger logger)
    {
        Directory.CreateDirectory(options.DataRoot);
        string tokenPath = Path.Combine(options.DataRoot, InitialGatewayTokenFileName);

        if (!string.IsNullOrWhiteSpace(options.GatewayToken))
        {
            LogToken(logger, "Gateway Token 已从 BUILD_SERVER_GATEWAY_TOKEN 或配置文件读取，不重新生成。", options.GatewayToken, tokenPath);
            return;
        }

        string? existingToken = ReadExistingToken(tokenPath);
        if (!string.IsNullOrWhiteSpace(existingToken))
        {
            options.GatewayToken = existingToken;
            LogToken(logger, "Gateway Token 已存在，复用数据目录中的 token。", options.GatewayToken, tokenPath);
            return;
        }

        options.GatewayToken = Ids.Secret();
        WriteTokenFile(tokenPath, options.GatewayToken);
        LogToken(logger, "未检测到 BUILD_SERVER_GATEWAY_TOKEN，已自动生成 Gateway Token。", options.GatewayToken, tokenPath);
    }

    private static string? ReadExistingToken(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (string line in File.ReadLines(path))
        {
            string value = line.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('#'))
            {
                continue;
            }

            const string gatewayTokenPrefix = "gateway token:";
            if (value.StartsWith(gatewayTokenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[gatewayTokenPrefix.Length..].Trim().Trim('"');
            }

            const string exportPrefix = "export BUILD_SERVER_GATEWAY_TOKEN=";
            if (value.StartsWith(exportPrefix, StringComparison.Ordinal))
            {
                return value[exportPrefix.Length..].Trim().Trim('"');
            }

            const string powershellPrefix = "$env:BUILD_SERVER_GATEWAY_TOKEN=";
            if (value.StartsWith(powershellPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[powershellPrefix.Length..].Trim().Trim('"');
            }
        }

        return null;
    }

    private static void WriteTokenFile(string path, string token)
    {
        File.WriteAllText(
            path,
            string.Join(Environment.NewLine, [
                $"gateway token: {token}",
                $"export BUILD_SERVER_GATEWAY_TOKEN=\"{token}\"",
                $"$env:BUILD_SERVER_GATEWAY_TOKEN=\"{token}\"",
                ""
            ]));
        TryRestrictSecretFile(path);
    }

    private static void LogToken(ILogger logger, string message, string token, string path)
    {
        logger.LogWarning("{Message}", message);
        logger.LogWarning("Gateway Token 文件: {Path}", path);
        logger.LogWarning("LinuxGateway 添加设备时 Gateway Token 填: {Token}", token);
        logger.LogWarning("macOS/Linux 可复制: export BUILD_SERVER_GATEWAY_TOKEN=\"{Token}\"", token);
        logger.LogWarning("Windows PowerShell 可复制: $env:BUILD_SERVER_GATEWAY_TOKEN=\"{Token}\"", token);
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
            // 权限收紧失败不阻止服务启动；部署时仍建议保护数据目录。
        }
    }
}
