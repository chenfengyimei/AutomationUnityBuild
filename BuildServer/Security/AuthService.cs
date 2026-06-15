using System.Security.Cryptography;
using System.Text;
using BuildServer.Persistence;

namespace BuildServer.Security;

public sealed class AuthService(JsonDatabase database, BuildServerOptions options)
{
    public const string CookieName = "aub_session";

    public async Task SeedDefaultsAsync()
    {
        string adminPassword = Environment.GetEnvironmentVariable("BUILD_SERVER_ADMIN_PASSWORD") ?? Ids.Secret();
        string agentToken = Environment.GetEnvironmentVariable("BUILD_SERVER_AGENT_TOKEN") ?? Ids.Secret();
        bool generatedAdminPassword = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILD_SERVER_ADMIN_PASSWORD"));
        bool generatedAgentToken = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILD_SERVER_AGENT_TOKEN"));
        bool adminCreated = false;
        bool agentCreated = false;

        await database.UpdateAsync(db =>
        {
            UserRecord admin = db.Users.FirstOrDefault(user => user.Role == Roles.Admin) ?? new UserRecord
            {
                Id = Ids.New("usr"),
                UserName = "admin",
                DisplayName = "管理员",
                Role = Roles.Admin,
                CreatedAt = DateTimeOffset.Now
            };

            if (!db.Users.Any(user => user.Id == admin.Id))
            {
                admin.PasswordHash = PasswordHasher.Hash(adminPassword);
                db.Users.Add(admin);
                adminCreated = true;
            }

            UserRecord agentUser = db.Users.FirstOrDefault(user => user.Role == Roles.Agent) ?? new UserRecord
            {
                Id = Ids.New("usr"),
                UserName = "agent",
                DisplayName = "MCP Agent",
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),
                Role = Roles.Agent,
                CreatedAt = DateTimeOffset.Now
            };

            if (!db.Users.Any(user => user.Id == agentUser.Id))
            {
                db.Users.Add(agentUser);
            }

            if (db.McpClients.Count == 0)
            {
                db.McpClients.Add(new McpClientRecord
                {
                    Id = Ids.New("mcp"),
                    Name = "default-agent",
                    TokenHash = PasswordHasher.HashToken(agentToken),
                    UserId = agentUser.Id,
                    CanStartBuild = true,
                    AllowFullBuild = false,
                    Enabled = true,
                    CreatedAt = DateTimeOffset.Now
                });
                agentCreated = true;
            }

            AddAudit(db, admin.Id, admin.UserName, "seed-defaults", "system", "build-server", "初始化默认管理员、Agent 和本机配置。");
        });

        WriteInitialSecretFile("initial-admin.txt", generatedAdminPassword && adminCreated, $"admin password: {adminPassword}");
        WriteInitialSecretFile("initial-agent-token.txt", generatedAgentToken && agentCreated, $"agent token: {agentToken}");
    }

    public async Task<UserRecord?> ValidateLoginAsync(string userName, string password)
    {
        return await database.ReadAsync(db =>
        {
            UserRecord? user = db.Users.FirstOrDefault(user =>
                user.Enabled &&
                string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase));
            return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        });
    }

    public async Task<string> CreateSessionAsync(UserRecord user)
    {
        string token = Ids.Secret();
        string tokenHash = PasswordHasher.HashToken(token);
        await database.UpdateAsync(db =>
        {
            db.Sessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.Now || session.UserId == user.Id);
            db.Sessions.Add(new SessionRecord
            {
                TokenHash = tokenHash,
                UserId = user.Id,
                ExpiresAt = DateTimeOffset.Now.AddDays(7)
            });
            AddAudit(db, user.Id, user.UserName, "login", "user", user.Id, "用户登录。");
        });
        return token;
    }

    public async Task LogoutAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        string tokenHash = PasswordHasher.HashToken(token);
        await database.UpdateAsync(db => db.Sessions.RemoveAll(session => session.TokenHash == tokenHash));
    }

    public async Task<CurrentUser?> GetUserAsync(HttpContext context)
    {
        string? token = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(token) &&
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = context.Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string tokenHash = PasswordHasher.HashToken(token);
        return await database.UpdateAsync(db =>
        {
            db.Sessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.Now);
            SessionRecord? session = db.Sessions.FirstOrDefault(session => session.TokenHash == tokenHash);
            if (session is null)
            {
                return null;
            }

            UserRecord? user = db.Users.FirstOrDefault(user => user.Id == session.UserId && user.Enabled);
            return user is null ? null : ToCurrentUser(user);
        });
    }

    public async Task<(CurrentUser User, McpClientRecord Client)?> GetMcpUserAsync(HttpContext context)
    {
        string token = context.Request.Headers["X-Agent-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            token = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? context.Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim()
                : "";
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string tokenHash = PasswordHasher.HashToken(token);
        return await database.ReadAsync<(CurrentUser User, McpClientRecord Client)?>(db =>
        {
            McpClientRecord? client = db.McpClients.FirstOrDefault(client => client.Enabled && client.TokenHash == tokenHash);
            if (client is null)
            {
                return null;
            }

            UserRecord? user = db.Users.FirstOrDefault(user => user.Id == client.UserId && user.Enabled);
            return user is null ? null : (ToCurrentUser(user), client);
        });
    }

    public static bool CanBuild(CurrentUser user)
    {
        return user.Role is Roles.Admin or Roles.ProjectOwner or Roles.Builder or Roles.Agent;
    }

    public static bool CanManage(CurrentUser user)
    {
        return user.Role is Roles.Admin or Roles.ProjectOwner;
    }

    public static bool IsAdmin(CurrentUser user)
    {
        return user.Role == Roles.Admin;
    }

    public static CurrentUser ToCurrentUser(UserRecord user)
    {
        return new CurrentUser(user.Id, user.UserName, user.DisplayName, user.Role);
    }

    public static void AddAudit(BuildServerDatabase db, string userId, string userName, string action, string targetType, string targetId, string details)
    {
        db.AuditLogs.Add(new AuditLogRecord
        {
            Id = Ids.New("aud"),
            UserId = userId,
            UserName = userName,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
            CreatedAt = DateTimeOffset.Now
        });

        if (db.AuditLogs.Count > 5000)
        {
            db.AuditLogs = db.AuditLogs
                .OrderByDescending(item => item.CreatedAt)
                .Take(5000)
                .OrderBy(item => item.CreatedAt)
                .ToList();
        }
    }

    private void WriteInitialSecretFile(string fileName, bool shouldWrite, string content)
    {
        if (!shouldWrite)
        {
            return;
        }

        Directory.CreateDirectory(options.DataRoot);
        string path = Path.Combine(options.DataRoot, fileName);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content + Environment.NewLine);
            TryRestrictSecretFile(path);
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
            // 文件权限收紧失败不阻止服务启动；部署文档会提醒手动保护数据目录。
        }
    }
}

public static class PasswordHasher
{
    private const int Pbkdf2Iterations = 210_000;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            32);
        return $"pbkdf2-sha256:{Pbkdf2Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        string[] parts = storedHash.Split(':');
        if (parts.Length == 4 && parts[0] == "pbkdf2-sha256")
        {
            int iterations = int.Parse(parts[1]);
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        return VerifyLegacySha256(password, storedHash);
    }

    public static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        string[] parts = storedHash.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] expected = Convert.FromBase64String(parts[1]);
        byte[] actual = SHA256.HashData(salt.Concat(Encoding.UTF8.GetBytes(password)).ToArray());
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public static class Ids
{
    public static string New(string prefix)
    {
        return $"{prefix}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }

    public static string Secret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
