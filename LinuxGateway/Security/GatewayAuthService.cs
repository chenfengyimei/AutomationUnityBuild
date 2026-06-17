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
        string password = options.AdminPassword;
        bool generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = Ids.Secret();
            generated = true;
        }

        string passwordHash = PasswordHasher.Hash(password);
        bool passwordHashWritten = false;
        if (!File.Exists(PasswordHashPath) || !string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            await File.WriteAllTextAsync(PasswordHashPath, passwordHash + Environment.NewLine);
            TryRestrictSecretFile(PasswordHashPath);
            passwordHashWritten = true;
        }

        if (generated && passwordHashWritten && !File.Exists(PasswordPath))
        {
            await File.WriteAllTextAsync(PasswordPath, $"admin password: {password}{Environment.NewLine}");
            TryRestrictSecretFile(PasswordPath);
        }
    }

    public async Task<bool> ValidateLoginAsync(string userName, string password)
    {
        if (!string.Equals(userName.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(PasswordHashPath))
        {
            return false;
        }

        string normalized = NormalizeInitialAdminPasswordInput(password);
        string hash = (await File.ReadAllTextAsync(PasswordHashPath)).Trim();
        return PasswordHasher.Verify(normalized, hash);
    }

    public async Task<string> CreateSessionAsync()
    {
        string token = Ids.Secret();
        string tokenHash = PasswordHasher.HashToken(token);
        await database.UpdateAsync(db =>
        {
            db.Sessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.Now);
            db.Sessions.Add(new GatewaySessionRecord
            {
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.Now.AddDays(7)
            });
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
        bool exists = await database.UpdateAsync(db =>
        {
            db.Sessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.Now);
            return db.Sessions.Any(session => session.TokenHash == tokenHash);
        });
        return exists ? new CurrentGatewayUser("admin", "管理员") : null;
    }

    private static string NormalizeInitialAdminPasswordInput(string password)
    {
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
