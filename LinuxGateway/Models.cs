namespace LinuxGateway;

public sealed class GatewayDatabase
{
    public int SchemaVersion { get; set; } = 1;
    public List<GatewayNodeRecord> Nodes { get; set; } = [];
    public List<GatewayJobRecord> Jobs { get; set; } = [];
    public List<GatewaySessionRecord> Sessions { get; set; } = [];
}

public sealed class GatewayNodeRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string GatewayToken { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastSeenAt { get; set; }
    public string LastStatus { get; set; } = "Unknown";
    public string LastError { get; set; } = "";
}

public sealed class GatewayJobRecord
{
    public string Id { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string RemoteJobId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ConfigId { get; set; } = "";
    public string ConfigName { get; set; } = "";
    public string BuildPlatform { get; set; } = "ios";
    public string Branch { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public bool DryRun { get; set; }
    public string Status { get; set; } = "Queued";
    public string Error { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class GatewaySessionRecord
{
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed record LoginRequest(string UserName, string Password);

public sealed record CurrentGatewayUser(string UserName, string DisplayName);

public sealed record GatewayNodeRequest(
    string? Id,
    string Name,
    string BaseUrl,
    string GatewayToken,
    string[]? Platforms,
    bool Enabled = true);

public sealed record GatewayStartBuildRequest(
    string NodeId,
    string ProjectId,
    string ConfigId,
    string? Branch,
    string? BuildNumber,
    bool DryRun = false,
    bool SkipGit = false,
    bool SkipUnity = false,
    bool SkipXcode = false,
    bool AllowNonMac = false,
    string? Notes = null);

public sealed record RemoteStartBuildRequest(
    string ProjectId,
    string ConfigId,
    string? Branch,
    string? BuildNumber,
    bool DryRun,
    bool SkipGit,
    bool SkipUnity,
    bool SkipXcode,
    bool AllowNonMac,
    string? Notes);

public sealed class RemoteNodeInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HostName { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public string PublicBaseUrl { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public List<RemoteProjectSummary> Projects { get; set; } = [];
    public List<RemoteConfigSummary> Configs { get; set; } = [];
    public List<RemoteBuildJobRecord> Jobs { get; set; } = [];
}

public sealed class RemoteProjectSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public List<string> AllowedBranches { get; set; } = [];
    public string DefaultBuildPlatform { get; set; } = "ios";
}

public sealed class RemoteConfigSummary
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string BuildPlatform { get; set; } = "ios";
    public bool AllowMcpBuild { get; set; }
}

public sealed class RemoteBuildJobRecord
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ConfigId { get; set; } = "";
    public string Status { get; set; } = "";
    public string BuildPlatform { get; set; } = "ios";
    public string Branch { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public bool DryRun { get; set; }
    public string Error { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class RemoteJobDetails
{
    public RemoteBuildJobRecord? Job { get; set; }
    public List<RemoteArtifactRecord> Artifacts { get; set; } = [];
}

public sealed class RemoteArtifactRecord
{
    public string Id { get; set; } = "";
    public string JobId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class GatewayNodeView
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public bool Enabled { get; set; }
    public bool TokenConfigured { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public string LastStatus { get; set; } = "Unknown";
    public string LastError { get; set; } = "";
    public RemoteNodeInfo? Remote { get; set; }
}
