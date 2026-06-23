using System.Net;
using System.Text;
using LinuxGateway;
using LinuxGateway.Services;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class NodeGatewayClientTests
{
    [Fact]
    public async Task GetHealthAsync_RetriesTransientGetFailure()
    {
        var handler = new QueueHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("busy", Encoding.UTF8, "text/plain")
            },
            _ => JsonResponse("""{"ok":true,"machine":"mac-1","name":"Mac","platforms":["ios"]}"""));
        var client = new NodeGatewayClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });

        RemoteGatewayHealth health = await client.GetHealthAsync(Node());

        Assert.True(health.Ok);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task StartBuildAsync_DoesNotRetryPostFailure()
    {
        var handler = new QueueHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("busy", Encoding.UTF8, "text/plain")
        });
        var client = new NodeGatewayClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
        var request = new RemoteStartBuildRequest(
            "project-1",
            "config-1",
            Branch: null,
            BuildNumber: null,
            DryRun: true,
            SkipGit: false,
            SkipUnity: false,
            SkipXcode: false,
            AllowNonMac: true,
            ClientRequestId: "req-1",
            Notes: null);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartBuildAsync(Node(), request));

        Assert.Contains("503", ex.Message);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task GetNodeAsync_UsesProblemDetailsMessage()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            """{"title":"Bad request","detail":"Gateway Token 无效。","status":401,"code":"unauthorized","traceId":"trace-1"}""",
            HttpStatusCode.Unauthorized,
            "application/problem+json"));
        var client = new NodeGatewayClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetNodeAsync(Node()));

        Assert.Contains("Gateway Token 无效。", ex.Message);
        Assert.DoesNotContain("secret-token", ex.Message);
    }

    private static GatewayNodeRecord Node()
    {
        return new GatewayNodeRecord
        {
            Id = "node-1",
            Name = "Mac",
            BaseUrl = "http://node.local",
            GatewayToken = "secret-token",
            Enabled = true
        };
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK, string mediaType = "application/json")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
    }

    private sealed class QueueHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _index;

        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Func<HttpRequestMessage, HttpResponseMessage> response = responses[Math.Min(_index, responses.Length - 1)];
            _index++;
            return Task.FromResult(response(request));
        }
    }
}
