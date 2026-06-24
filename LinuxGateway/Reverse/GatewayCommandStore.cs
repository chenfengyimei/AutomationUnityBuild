using LinuxGateway.Persistence;
using LinuxGateway.Security;

namespace LinuxGateway.Reverse;

public sealed class GatewayCommandStore(JsonGatewayDatabase database)
{
    private static readonly TimeSpan SentRecoveryDelay = TimeSpan.FromSeconds(10);

    public async Task<GatewayCommandRecord> CreateAsync(string nodeId, string type, string clientRequestId, string correlationId, object payload)
    {
        return await database.UpdateAsync(db =>
        {
            var cmd = new GatewayCommandRecord
            {
                Id = correlationId,
                NodeId = nodeId,
                Type = type,
                Status = ReverseCommandStatus.Pending,
                ClientRequestId = clientRequestId,
                CorrelationId = correlationId,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload, ReverseProtocol.JsonOptions),
                CreatedAt = DateTimeOffset.Now
            };
            db.Commands.Add(cmd);
            db.Commands.RemoveAll(c => c.CompletedAt.HasValue && c.CompletedAt < DateTimeOffset.Now.AddDays(-7));
            return cmd;
        });
    }

    public async Task MarkSentAsync(string commandId)
    {
        await database.UpdateAsync(db =>
        {
            GatewayCommandRecord? cmd = db.Commands.FirstOrDefault(c => c.Id == commandId);
            if (cmd is not null)
            {
                cmd.Status = ReverseCommandStatus.Sent;
                cmd.SentAt = DateTimeOffset.Now;
                cmd.AttemptCount++;
            }
        });
    }

    public async Task MarkCompletedAsync(string commandId, string? resultJson, string? error)
    {
        await database.UpdateAsync(db =>
        {
            GatewayCommandRecord? cmd = db.Commands.FirstOrDefault(c => c.Id == commandId);
            if (cmd is not null)
            {
                cmd.Status = string.IsNullOrEmpty(error) ? ReverseCommandStatus.Completed : ReverseCommandStatus.Failed;
                cmd.ResultJson = resultJson ?? "";
                cmd.Error = error ?? "";
                cmd.CompletedAt = DateTimeOffset.Now;
            }
        });
    }

    public async Task MarkCanceledAsync(string commandId, string reason)
    {
        await database.UpdateAsync(db =>
        {
            GatewayCommandRecord? cmd = db.Commands.FirstOrDefault(c => c.Id == commandId);
            if (cmd is not null)
            {
                cmd.Status = ReverseCommandStatus.Canceled;
                cmd.Error = reason;
                cmd.CompletedAt = DateTimeOffset.Now;
            }
        });
    }

    public async Task<List<GatewayCommandRecord>> GetPendingForNodeAsync(string nodeId)
    {
        DateTimeOffset recoverSentBefore = DateTimeOffset.Now.Subtract(SentRecoveryDelay);
        return await database.ReadAsync(db => db.Commands
            .Where(c => c.NodeId == nodeId &&
                (c.Status == ReverseCommandStatus.Pending ||
                 (c.Status == ReverseCommandStatus.Sent && (!c.SentAt.HasValue || c.SentAt < recoverSentBefore))))
            .OrderBy(c => c.CreatedAt)
            .ToList());
    }

    public async Task<GatewayCommandRecord?> GetAsync(string commandId)
    {
        return await database.ReadAsync(db => db.Commands.FirstOrDefault(c => c.Id == commandId));
    }
}
