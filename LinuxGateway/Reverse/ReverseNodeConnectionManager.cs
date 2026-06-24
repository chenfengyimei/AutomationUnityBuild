using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace LinuxGateway.Reverse;

public sealed class ReverseConnection : IDisposable
{
    public string NodeId { get; }
    public WebSocket Socket { get; }
    public DateTimeOffset ConnectedAt { get; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public string RemoteIp { get; }
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public ReverseConnection(string nodeId, WebSocket socket, string remoteIp)
    {
        NodeId = nodeId;
        Socket = socket;
        RemoteIp = remoteIp;
        ConnectedAt = DateTimeOffset.Now;
        LastHeartbeatAt = DateTimeOffset.Now;
    }

    public async Task SendAsync(ReverseMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposed || Socket.State is not WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket 连接已关闭。");
        }

        string json = JsonSerializer.Serialize(message, ReverseProtocol.JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sendLock.Dispose();
        if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { Socket.Dispose(); } catch { }
        }
    }
}

public sealed class ReverseNodeConnectionManager(ILogger<ReverseNodeConnectionManager> logger)
{
    private readonly ConcurrentDictionary<string, ReverseConnection> _connections = new();
    private readonly ConcurrentDictionary<string, Channel<ReverseMessage>> _pendingCommands = new();

    public IReadOnlyDictionary<string, ReverseConnection> Connections => _connections;

    public bool IsOnline(string nodeId)
    {
        return _connections.TryGetValue(nodeId, out ReverseConnection? conn) &&
               conn.Socket.State is WebSocketState.Open;
    }

    public ReverseConnection? GetConnection(string nodeId)
    {
        return _connections.TryGetValue(nodeId, out ReverseConnection? conn) ? conn : null;
    }

    public List<string> GetOnlineNodeIds()
    {
        return _connections
            .Where(kvp => kvp.Value.Socket.State is WebSocketState.Open)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public ReverseConnection AddOrReplace(string nodeId, WebSocket socket, string remoteIp)
    {
        ReverseConnection newConn = new(nodeId, socket, remoteIp);

        if (_connections.TryRemove(nodeId, out ReverseConnection? oldConn))
        {
            logger.LogInformation("Node {NodeId} new connection replacing old connection (old IP: {OldIp}, new IP: {NewIp})", nodeId, oldConn.RemoteIp, remoteIp);
            try
            {
                if (oldConn.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    oldConn.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Replaced by new connection", CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch { }
            oldConn.Dispose();
        }

        _connections[nodeId] = newConn;
        logger.LogInformation("Node {NodeId} connected from {RemoteIp}, total online: {Count}", nodeId, remoteIp, _connections.Count);
        return newConn;
    }

    public void Remove(string nodeId)
    {
        if (_connections.TryRemove(nodeId, out ReverseConnection? conn))
        {
            conn.Dispose();
            logger.LogInformation("Node {NodeId} disconnected, total online: {Count}", nodeId, _connections.Count);
        }
    }

    public void UpdateHeartbeat(string nodeId)
    {
        if (_connections.TryGetValue(nodeId, out ReverseConnection? conn))
        {
            conn.LastHeartbeatAt = DateTimeOffset.Now;
        }
    }

    public async Task<bool> SendAsync(string nodeId, ReverseMessage message, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(nodeId, out ReverseConnection? conn))
        {
            return false;
        }

        try
        {
            await conn.SendAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send message to node {NodeId}", nodeId);
            Remove(nodeId);
            return false;
        }
    }
}
