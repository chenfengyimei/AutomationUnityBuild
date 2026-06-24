using System.Security.Cryptography;
using System.Text;
using LinuxGateway.Persistence;
using LinuxGateway.Security;

namespace LinuxGateway.Reverse;

public sealed class EnrollmentService(JsonGatewayDatabase database, ILogger<EnrollmentService> logger)
{
    public async Task<EnrollmentTokenResult> CreateTokenAsync(CurrentGatewayUser admin, string nodeNameHint, TimeSpan? validity = null)
    {
        TimeSpan expires = validity ?? TimeSpan.FromHours(24);
        string token = Ids.Secret();
        string tokenHash = PasswordHasher.HashToken(token);

        EnrollmentTokenRecord record = await database.UpdateAsync(db =>
        {
            db.EnrollmentTokens.RemoveAll(t => t.ExpiresAt < DateTimeOffset.Now && !t.UsedAt.HasValue);
            var entry = new EnrollmentTokenRecord
            {
                Id = Ids.New("tok"),
                TokenHash = tokenHash,
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.UserName,
                ExpiresAt = DateTimeOffset.Now.Add(expires),
                NodeNameHint = nodeNameHint ?? ""
            };
            db.EnrollmentTokens.Add(entry);
            GatewayAuthService.AddAudit(db, admin.Id, admin.UserName, "enrollment-token.create", "enrollment-token", entry.Id, $"创建注册 Token（节点提示: {nodeNameHint}）");
            return entry;
        });

        logger.LogInformation("Created enrollment token {TokenId} by {User}", record.Id, admin.UserName);
        return new EnrollmentTokenResult(token, record.Id, record.ExpiresAt);
    }

    public async Task<EnrollmentResult> EnrollAsync(string enrollmentToken, string nodeName, string[] platforms, string agentVersion)
    {
        if (string.IsNullOrWhiteSpace(enrollmentToken))
        {
            throw new ArgumentException("Enrollment Token 不能为空。");
        }

        string tokenHash = PasswordHasher.HashToken(enrollmentToken.Trim());

        EnrollmentResult result = await database.UpdateAsync(db =>
        {
            EnrollmentTokenRecord? tokenRecord = db.EnrollmentTokens
                .FirstOrDefault(t => t.TokenHash == tokenHash);

            if (tokenRecord is null)
            {
                throw new UnauthorizedAccessException("Enrollment Token 无效。");
            }

            if (tokenRecord.UsedAt.HasValue)
            {
                throw new InvalidOperationException("Enrollment Token 已被使用。");
            }

            if (tokenRecord.Revoked)
            {
                throw new InvalidOperationException("Enrollment Token 已被吊销。");
            }

            if (tokenRecord.ExpiresAt < DateTimeOffset.Now)
            {
                throw new InvalidOperationException("Enrollment Token 已过期。");
            }

            string nodeId = Ids.New("node");
            string credential = Ids.Secret();
            string credentialHash = PasswordHasher.HashToken(credential);

            tokenRecord.UsedAt = DateTimeOffset.Now;
            tokenRecord.UsedByNodeId = nodeId;

            var node = new GatewayNodeRecord
            {
                Id = nodeId,
                Name = string.IsNullOrWhiteSpace(nodeName) ? (!string.IsNullOrWhiteSpace(tokenRecord.NodeNameHint) ? tokenRecord.NodeNameHint : $"Reverse-{nodeId[^8..]}") : nodeName.Trim(),
                BaseUrl = "",
                GatewayToken = "",
                Platforms = platforms.Length > 0 ? platforms.ToList() : ["android"],
                Enabled = true,
                ConnectionMode = ReverseConnectionModes.Reverse,
                AgentVersion = agentVersion ?? "1.0.0",
                ProtocolVersion = ReverseProtocol.Version,
                LastHeartbeatAt = DateTimeOffset.Now,
                ConnectionStatus = ReverseConnectionStatus.Online
            };
            db.Nodes.Add(node);

            db.ReverseCredentials.Add(new ReverseNodeCredentialRecord
            {
                NodeId = nodeId,
                CredentialHash = credentialHash,
                CreatedAt = DateTimeOffset.Now
            });

            GatewayAuthService.AddAudit(db, tokenRecord.CreatedByUserId, tokenRecord.CreatedByUserName, "node.enroll", "node", nodeId, $"反向节点注册: {node.Name}");

            return new EnrollmentResult(nodeId, credential, node.Name);
        });

        logger.LogInformation("Node enrolled: {NodeId} ({NodeName})", result.NodeId, result.NodeName);
        return result;
    }

    public async Task<bool> RevokeCredentialAsync(string nodeId, CurrentGatewayUser admin)
    {
        await database.UpdateAsync(db =>
        {
            ReverseNodeCredentialRecord? cred = db.ReverseCredentials.FirstOrDefault(c => c.NodeId == nodeId);
            if (cred is not null)
            {
                cred.RevokedAt = DateTimeOffset.Now;
            }

            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.CredentialRevokedAt = DateTimeOffset.Now;
                node.ConnectionStatus = ReverseConnectionStatus.Offline;
            }

            GatewayAuthService.AddAudit(db, admin.Id, admin.UserName, "node.credential-revoke", "node", nodeId, $"吊销反向节点凭据: {node?.Name ?? nodeId}");
        });

        logger.LogInformation("Credential revoked for node {NodeId}", nodeId);
        return true;
    }

    public async Task<bool> ValidateCredentialAsync(string nodeId, string credential)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(credential))
        {
            return false;
        }

        string credentialHash = PasswordHasher.HashToken(credential.Trim());

        return await database.ReadAsync(db =>
        {
            ReverseNodeCredentialRecord? cred = db.ReverseCredentials.FirstOrDefault(c => c.NodeId == nodeId);
            if (cred is null || cred.RevokedAt.HasValue)
            {
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cred.CredentialHash),
                Encoding.UTF8.GetBytes(credentialHash)))
            {
                return false;
            }

            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            return node is not null && node.Enabled && !node.CredentialRevokedAt.HasValue;
        });
    }

    public async Task<List<EnrollmentTokenRecord>> ListTokensAsync()
    {
        return await database.ReadAsync(db => db.EnrollmentTokens
            .Where(t => !t.UsedAt.HasValue && !t.Revoked && t.ExpiresAt > DateTimeOffset.Now)
            .OrderByDescending(t => t.CreatedAt)
            .ToList());
    }
}

public sealed record EnrollmentTokenResult(string Token, string TokenId, DateTimeOffset ExpiresAt);

public sealed record EnrollmentResult(string NodeId, string Credential, string NodeName);
