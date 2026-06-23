using System.Reflection;
using BuildServer;
using BuildServer.Persistence;
using BuildServer.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class BuildServerApiRoutesTests
{
    [Fact]
    public async Task UpdateUserAsync_DoesNotAllowRootAdminToBeDemoted_WhenAnotherAdminExists()
    {
        string root = TestRoot();
        try
        {
            BuildServerOptions options = Options(root);
            JsonDatabase database = await DatabaseAsync(options);
            UserRecord admin = await SeedAdminsAsync(database);
            var auth = new AuthService(database, options);
            string token = await auth.CreateSessionAsync(admin);

            var request = new UserRequest("admin", "Admin", Roles.Viewer, Password: null, Enabled: true);

            IResult result = await InvokeUpdateUserAsync(admin.Id, request, ContextWithBearer(token), auth, database);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            UserRecord stored = await database.ReadAsync(db => db.Users.Single(user => user.Id == admin.Id));
            Assert.Equal("admin", stored.UserName);
            Assert.Equal(Roles.Admin, stored.Role);
            Assert.True(stored.Enabled);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DeleteUserAsync_DoesNotAllowRootAdminToBeDisabled_WhenAnotherAdminExists()
    {
        string root = TestRoot();
        try
        {
            BuildServerOptions options = Options(root);
            JsonDatabase database = await DatabaseAsync(options);
            UserRecord admin = await SeedAdminsAsync(database);
            var auth = new AuthService(database, options);
            string token = await auth.CreateSessionAsync(admin);

            IResult result = await InvokeDeleteUserAsync(admin.Id, ContextWithBearer(token), auth, database);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            UserRecord stored = await database.ReadAsync(db => db.Users.Single(user => user.Id == admin.Id));
            Assert.Equal(Roles.Admin, stored.Role);
            Assert.True(stored.Enabled);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task<IResult> InvokeUpdateUserAsync(
        string userId,
        UserRequest request,
        HttpContext context,
        AuthService auth,
        JsonDatabase database)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("UpdateUserAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("UpdateUserAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [userId, request, context, auth, database])
            ?? throw new InvalidOperationException("UpdateUserAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<IResult> InvokeDeleteUserAsync(
        string userId,
        HttpContext context,
        AuthService auth,
        JsonDatabase database)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("DeleteUserAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteUserAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [userId, context, auth, database])
            ?? throw new InvalidOperationException("DeleteUserAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<UserRecord> SeedAdminsAsync(JsonDatabase database)
    {
        var admin = new UserRecord
        {
            Id = "usr-admin",
            UserName = "admin",
            DisplayName = "Admin",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("Passw0rd!one"),
            Enabled = true,
            CreatedAt = DateTimeOffset.Now
        };
        var backup = new UserRecord
        {
            Id = "usr-backup",
            UserName = "backup-admin",
            DisplayName = "Backup Admin",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("Passw0rd!two"),
            Enabled = true,
            CreatedAt = DateTimeOffset.Now
        };

        await database.UpdateAsync(db =>
        {
            db.Users.Add(admin);
            db.Users.Add(backup);
        });

        return admin;
    }

    private static async Task<JsonDatabase> DatabaseAsync(BuildServerOptions options)
    {
        var database = new JsonDatabase(options);
        await database.InitializeAsync();
        return database;
    }

    private static BuildServerOptions Options(string root)
    {
        return new BuildServerOptions { DataRoot = root };
    }

    private static DefaultHttpContext ContextWithBearer(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    private static string TestRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"build-server-api-{Guid.NewGuid():N}");
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
