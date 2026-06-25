using System.Net;
using System.Reflection;
using System.Text;
using LinuxGateway;
using LinuxGateway.Persistence;
using LinuxGateway.Reverse;
using LinuxGateway.Security;
using LinuxGateway.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class GatewayApiRoutesTests
{
    [Fact]
    public async Task UpdateUserAsync_DoesNotAllowRootAdminToBeDemoted_WhenAnotherAdminExists()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root);
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            GatewayUserRecord admin = await SeedGatewayAdminsAsync(database, auth);
            string token = await auth.CreateSessionAsync(admin);

            var request = new GatewayUserRequest("admin", "Admin", GatewayRoles.Viewer, Password: null, Enabled: true);

            IResult result = await InvokeUpdateUserAsync(admin.Id, request, ContextWithBearer(token), auth, database);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            GatewayUserRecord stored = await database.ReadAsync(db => db.Users.Single(user => user.Id == admin.Id));
            Assert.Equal("admin", stored.UserName);
            Assert.Equal(GatewayRoles.Admin, stored.Role);
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
            LinuxGatewayOptions options = Options(root);
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            GatewayUserRecord admin = await SeedGatewayAdminsAsync(database, auth);
            string token = await auth.CreateSessionAsync(admin);

            IResult result = await InvokeDeleteUserAsync(admin.Id, ContextWithBearer(token), auth, database);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            GatewayUserRecord stored = await database.ReadAsync(db => db.Users.Single(user => user.Id == admin.Id));
            Assert.Equal(GatewayRoles.Admin, stored.Role);
            Assert.True(stored.Enabled);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task StartBuildAsync_DoesNotCallRemoteClient_WhenNodeIsDisabled()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root);
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            await auth.SeedAsync();

            GatewayUserRecord? admin = await auth.ValidateLoginAsync("admin", "Passw0rd!one");
            Assert.NotNull(admin);
            string token = await auth.CreateSessionAsync(admin);

            await database.UpdateAsync(db =>
            {
                db.Nodes.Add(new GatewayNodeRecord
                {
                    Id = "node-1",
                    Name = "Disabled Node",
                    BaseUrl = "http://node.local",
                    GatewayToken = "gateway-token",
                    Enabled = false,
                    CreatedAt = DateTimeOffset.Now
                });
            });

            var handler = new CountingHandler();
            var client = new NodeGatewayClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            var transportFactory = new NodeTransportFactory(new DirectNodeTransport(client), null!);
            var request = new GatewayStartBuildRequest(
                "node-1",
                "project-1",
                "config-1",
                Branch: null,
                BuildNumber: null,
                DryRun: true,
                ClientRequestId: "req-disabled-node");

            IResult result = await InvokeStartBuildAsync(request, ContextWithBearer(token), auth, database, transportFactory);

            Assert.Equal(0, handler.Count);
            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task StartBuildAsync_DoesNotCallRemoteStartTwice_ForSameClientRequestId()
    {
        string root = TestRoot();
        try
        {
            LinuxGatewayOptions options = Options(root);
            JsonGatewayDatabase database = await DatabaseAsync(options);
            var auth = new GatewayAuthService(database, options);
            await auth.SeedAsync();

            GatewayUserRecord? admin = await auth.ValidateLoginAsync("admin", "Passw0rd!one");
            Assert.NotNull(admin);
            string token = await auth.CreateSessionAsync(admin);

            await database.UpdateAsync(db =>
            {
                db.Nodes.Add(new GatewayNodeRecord
                {
                    Id = "node-1",
                    Name = "Enabled Node",
                    BaseUrl = "http://node.local",
                    GatewayToken = "gateway-token",
                    Platforms = ["android"],
                    Enabled = true,
                    CreatedAt = DateTimeOffset.Now
                });
            });

            var handler = new StartBuildHandler();
            var client = new NodeGatewayClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
            var transportFactory = new NodeTransportFactory(new DirectNodeTransport(client), null!);
            var request = new GatewayStartBuildRequest(
                "node-1",
                "project-1",
                "config-1",
                Branch: "main",
                BuildNumber: "42",
                DryRun: true,
                ClientRequestId: "req-same");

            IResult first = await InvokeStartBuildAsync(request, ContextWithBearer(token), auth, database, transportFactory);
            IResult second = await InvokeStartBuildAsync(request, ContextWithBearer(token), auth, database, transportFactory);

            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(first).StatusCode);
            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(second).StatusCode);
            Assert.Equal(1, handler.StartBuildCount);

            List<GatewayJobRecord> jobs = await database.ReadAsync(db => db.Jobs.ToList());
            GatewayJobRecord job = Assert.Single(jobs);
            Assert.Equal("req-same", job.ClientRequestId);
            Assert.Equal("remote-job-1", job.RemoteJobId);
            Assert.Equal(GatewayBuildStatuses.Queued, job.Status);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task<IResult> InvokeStartBuildAsync(
        GatewayStartBuildRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeTransportFactory transportFactory)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("StartBuildAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("StartBuildAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [request, context, auth, database, transportFactory])
            ?? throw new InvalidOperationException("StartBuildAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<IResult> InvokeUpdateUserAsync(
        string userId,
        GatewayUserRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
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
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("DeleteUserAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteUserAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [userId, context, auth, database])
            ?? throw new InvalidOperationException("DeleteUserAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<GatewayUserRecord> SeedGatewayAdminsAsync(JsonGatewayDatabase database, GatewayAuthService auth)
    {
        await auth.SeedAsync();
        GatewayUserRecord admin = await database.ReadAsync(db => db.Users.Single(user => user.UserName == "admin"));
        await database.UpdateAsync(db =>
        {
            db.Users.Add(new GatewayUserRecord
            {
                Id = "gusr-backup",
                UserName = "backup-admin",
                DisplayName = "Backup Admin",
                Role = GatewayRoles.Admin,
                PasswordHash = PasswordHasher.Hash("Passw0rd!two"),
                Enabled = true,
                CreatedAt = DateTimeOffset.Now
            });
        });

        return admin;
    }

    private static async Task<JsonGatewayDatabase> DatabaseAsync(LinuxGatewayOptions options)
    {
        var database = new JsonGatewayDatabase(options);
        await database.InitializeAsync();
        return database;
    }

    private static LinuxGatewayOptions Options(string root)
    {
        return new LinuxGatewayOptions
        {
            DataRoot = root,
            AdminPassword = "Passw0rd!one"
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
        return Path.Combine(Path.GetTempPath(), $"linux-gateway-api-{Guid.NewGuid():N}");
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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            throw new InvalidOperationException("Remote node should not be called for a disabled node.");
        }
    }

    private sealed class StartBuildHandler : HttpMessageHandler
    {
        public int StartBuildCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            if (path.EndsWith("/api/gateway/node", StringComparison.OrdinalIgnoreCase))
            {
                return Json(
                    """
                    {
                      "id": "node-1",
                      "name": "Enabled Node",
                      "platforms": ["android"],
                      "projects": [
                        { "id": "project-1", "name": "Project One", "defaultBranch": "main", "defaultBuildPlatform": "android" }
                      ],
                      "configs": [
                        { "id": "config-1", "projectId": "project-1", "name": "Android Release", "buildPlatform": "android" }
                      ]
                    }
                    """);
            }

            if (path.EndsWith("/api/gateway/builds", StringComparison.OrdinalIgnoreCase))
            {
                StartBuildCount++;
                _ = request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                return Json(
                    """
                    {
                      "id": "remote-job-1",
                      "projectId": "project-1",
                      "configId": "config-1",
                      "status": "Queued",
                      "buildPlatform": "android",
                      "branch": "main",
                      "buildNumber": "42",
                      "dryRun": true
                    }
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found", Encoding.UTF8, "text/plain")
            };
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
