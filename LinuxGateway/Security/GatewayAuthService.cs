using System.Security.Cryptography;
using System.Text;
using LinuxGateway.Persistence;

namespace LinuxGateway.Security;

public sealed class GatewayAuthService(JsonGatewayDatabase database, LinuxGatewayOptions options)
{
    public const string CookieName = "lgw_session";
    private string PasswordPath => Path.Combine(options.DataRoot, "initial-admin.txt");
    private string PasswordHashPath => Path.Combine(options.DataRoot, "admin-password.hash");

    public async Task SeedAsync()
    {
        Directory.CreateDirectory(options.DataRoot);
        string? configuredAdminPassword = string.IsNullOrWhiteSpace(options.AdminPassword) ? null : options.AdminPassword;
        string generatedPassword = Ids.Secret();
        string? legacyHash = File.Exists(PasswordHashPath) ? (await File.ReadAllTextAsync(PasswordHashPath)).Trim() : null;
        bool createdAdmin = false;
        bool generatedAdminPassword = configuredAdminPassword is null && string.IsNullOrWhiteSpace(legacyHash);
        string adminPasswordForNewInstall = configuredAdminPassword ?? generatedPassword;

        await database.UpdateAsync(db =>
        {
            GatewayUserRecord? admin = db.Users.FirstOrDefault(user => user.Role == GatewayRoles.Admin);
            if (admin is null)
            {
                admin = new GatewayUserRecord
                {
                    Id = Ids.New("gusr"),
                    UserName = "admin",
                    DisplayName = "管理员",
                    Role = GatewayRoles.Admin,
                    PasswordHash = configuredAdminPassword is not null
                        ? PasswordHasher.Hash(configuredAdminPassword)
                        : !string.IsNullOrWhiteSpace(legacyHash)
                            ? legacyHash
                            : PasswordHasher.Hash(adminPasswordForNewInstall),
                    Enabled = true,
                    CreatedAt = DateTimeOffset.Now
                };
                db.Users.Add(admin);
                createdAdmin = true;
                AddAudit(db, admin.Id, admin.UserName, "seed-admin", "user", admin.Id, "初始化 LinuxGateway 管理员。");
            }
            else if (configuredAdminPassword is not null && !PasswordHasher.Verify(configuredAdminPassword, admin.PasswordHash))
            {
                admin.PasswordHash = PasswordHasher.Hash(configuredAdminPassword);
                admin.Enabled = true;
                db.Sessions.RemoveAll(session => session.UserId == admin.Id);
                AddAudit(db, admin.Id, admin.UserName, "update-admin-password", "user", admin.Id, "通过 LINUX_GATEWAY_ADMIN_PASSWORD 更新管理员密码。");
            }
        });

        if (createdAdmin && generatedAdminPassword && !File.Exists(PasswordPath))
        {
            await File.WriteAllTextAsync(PasswordPath, $"admin password: {adminPasswordForNewInstall}{Environment.NewLine}");
            TryRestrictSecretFile(PasswordPath);
        }
    }

    public async Task<GatewayUserRecord?> ValidateLoginAsync(string userName, string password)
    {
        string normalizedUserName = userName.Trim();
        string normalizedPassword = NormalizeInitialAdminPasswordInput(normalizedUserName, password);
        return await database.ReadAsync(db =>
        {
            GatewayUserRecord? user = db.Users.FirstOrDefault(user =>
                user.Enabled &&
                string.Equals(user.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase));
            return user is not null && PasswordHasher.Verify(normalizedPassword, user.PasswordHash) ? user : null;
        });
    }

    public async Task<string> CreateSessionAsync(GatewayUserRecord user)
    {
        string token = Ids.Secret();
        string tokenHash = PasswordHasher.HashToken(token);
        await database.UpdateAsync(db =>
        {
            db.Sessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.Now || session.UserId == user.Id);
            db.Sessions.Add(new GatewaySessionRecord
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

    public async Task<CurrentGatewayUser?> GetUserAsync(HttpContext context)
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
            GatewaySessionRecord? session = db.Sessions.FirstOrDefault(session => session.TokenHash == tokenHash);
            if (session is null)
            {
                return null;
            }

            GatewayUserRecord? user = db.Users.FirstOrDefault(user => user.Id == session.UserId && user.Enabled);
            return user is null ? null : ToCurrentUser(user);
        });
    }

    public static bool IsAdmin(CurrentGatewayUser user)
    {
        return user.Role == GatewayRoles.Admin;
    }

    public static bool CanBuild(CurrentGatewayUser user)
    {
        return user.Role is GatewayRoles.Admin or GatewayRoles.Builder;
    }

    public static CurrentGatewayUser ToCurrentUser(GatewayUserRecord user)
    {
        return new CurrentGatewayUser(user.Id, user.UserName, user.DisplayName, user.Role);
    }

    public static void AddAudit(GatewayDatabase db, string userId, string userName, string action, string targetType, string targetId, string details)
    {
        db.AuditLogs.Add(new GatewayAuditLogRecord
        {
            Id = Ids.New("gaud"),
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

    private static string NormalizeInitialAdminPasswordInput(string userName, string password)
    {
        if (!string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return password;
        }

        const string adminPasswordPrefix = "admin password:";
        if (password.TrimStart().StartsWith(adminPasswordPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return password.TrimStart()[adminPasswordPrefix.Length..].Trim();
        }

        const string passwordPrefix = "password:";
        if (password.TrimStart().StartsWith(passwordPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return password.TrimStart()[passwordPrefix.Length..].Trim();
        }

        return password;
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
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256")
        {
            return false;
        }

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

    public static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
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
