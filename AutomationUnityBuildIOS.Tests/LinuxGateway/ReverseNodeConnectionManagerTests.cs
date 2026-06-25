using System.Net.WebSockets;
using LinuxGateway.Reverse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class ReverseNodeConnectionManagerTests
{
    [Fact]
    public void Remove_WithOldConnectionId_DoesNotRemoveReplacementConnection()
    {
        var manager = new ReverseNodeConnectionManager(NullLogger<ReverseNodeConnectionManager>.Instance);

        ReverseConnection first = manager.AddOrReplace("node-1", new ClientWebSocket(), "127.0.0.1");
        ReverseConnection second = manager.AddOrReplace("node-1", new ClientWebSocket(), "127.0.0.2");

        Assert.False(manager.Remove("node-1", first.ConnectionId));
        Assert.Same(second, manager.GetConnection("node-1"));

        Assert.True(manager.Remove("node-1", second.ConnectionId));
        Assert.Null(manager.GetConnection("node-1"));
    }
}
