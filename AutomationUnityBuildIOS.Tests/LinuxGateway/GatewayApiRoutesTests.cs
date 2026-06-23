using System.Reflection;
using LinuxGateway;
using LinuxGateway.Persistence;
using LinuxGateway.Security;
using LinuxGateway.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class GatewayApiRoutesTests
{
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
            var request = new GatewayStartBuildRequest(
                "node-1",
                "project-1",
                "config-1",
                Branch: null,
                BuildNumber: null,
                DryRun: true,
                ClientRequestId: "req-disabled-node");

            IResult result = await InvokeStartBuildAsync(request, ContextWithBearer(token), auth, database, client);

            Assert.Equal(0, handler.Count);
            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
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
        NodeGatewayClient client)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("StartBuildAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("StartBuildAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [request, context, auth, database, client])
            ?? throw new InvalidOperationException("StartBuildAsync route handler did not return a task.");
        return await task;
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
}
