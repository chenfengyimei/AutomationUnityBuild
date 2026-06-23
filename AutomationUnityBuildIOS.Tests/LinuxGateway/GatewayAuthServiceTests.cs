using LinuxGateway;
using LinuxGateway.Persistence;
using LinuxGateway.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class GatewayAuthServiceTests
{
    [Fact]
    public async Task SeedAsync_CreatesAdminUser_AndBearerSessionResolvesCurrentUser()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root, "Passw0rd!one");
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);

            await auth.SeedAsync();

            GatewayUserRecord? user = await auth.ValidateLoginAsync("admin", "Passw0rd!one");
            Assert.NotNull(user);
            Assert.Equal(GatewayRoles.Admin, user.Role);

            string token = await auth.CreateSessionAsync(user);
            CurrentGatewayUser? current = await auth.GetUserAsync(ContextWithBearer(token));

            Assert.NotNull(current);
            Assert.Equal("admin", current.UserName);
            Assert.True(GatewayAuthService.IsAdmin(current));
            Assert.True(GatewayAuthService.CanBuild(current));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SeedAsync_RotatesConfiguredAdminPassword_AndInvalidatesOldSessions()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root, "Passw0rd!one");
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            await auth.SeedAsync();

            GatewayUserRecord? firstUser = await auth.ValidateLoginAsync("admin", "Passw0rd!one");
            Assert.NotNull(firstUser);
            string oldToken = await auth.CreateSessionAsync(firstUser);

            options.AdminPassword = "Passw0rd!two";
            var rotatedAuth = new GatewayAuthService(database, options);
            await rotatedAuth.SeedAsync();

            Assert.Null(await rotatedAuth.ValidateLoginAsync("admin", "Passw0rd!one"));
            GatewayUserRecord? rotatedUser = await rotatedAuth.ValidateLoginAsync("admin", "Passw0rd!two");
            Assert.NotNull(rotatedUser);
            Assert.Null(await rotatedAuth.GetUserAsync(ContextWithBearer(oldToken)));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task GetUserAsync_RejectsDisabledUsersEvenWhenSessionExists()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root, "Passw0rd!one");
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            await auth.SeedAsync();

            GatewayUserRecord builder = await database.UpdateAsync(db =>
            {
                var user = new GatewayUserRecord
                {
                    Id = Ids.New("gusr"),
                    UserName = "builder",
                    DisplayName = "Builder",
                    Role = GatewayRoles.Builder,
                    PasswordHash = PasswordHasher.Hash("Passw0rd!builder"),
                    Enabled = true,
                    CreatedAt = DateTimeOffset.Now
                };
                db.Users.Add(user);
                return user;
            });

            string token = await auth.CreateSessionAsync(builder);
            await database.UpdateAsync(db =>
            {
                GatewayUserRecord stored = db.Users.Single(user => user.Id == builder.Id);
                stored.Enabled = false;
            });

            Assert.Null(await auth.GetUserAsync(ContextWithBearer(token)));
            Assert.False(GatewayAuthService.CanBuild(new CurrentGatewayUser("u", "viewer", "Viewer", GatewayRoles.Viewer)));
            Assert.True(GatewayAuthService.CanBuild(new CurrentGatewayUser("u", "builder", "Builder", GatewayRoles.Builder)));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task<JsonGatewayDatabase> DatabaseAsync(LinuxGatewayOptions options)
    {
        var database = new JsonGatewayDatabase(options);
        await database.InitializeAsync();
        return database;
    }

    private static LinuxGatewayOptions Options(string root, string adminPassword)
    {
        return new LinuxGatewayOptions
        {
            DataRoot = root,
            AdminPassword = adminPassword
        };
    }

    private static DefaultHttpContext ContextWithBearer(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    private static string TestRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"linux-gateway-auth-{Guid.NewGuid():N}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
