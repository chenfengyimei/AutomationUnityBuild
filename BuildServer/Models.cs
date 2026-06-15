namespace BuildServer;

public sealed class BuildServerDatabase
{
    public int SchemaVersion { get; set; } = 1;
    public List<UserRecord> Users { get; set; } = [];
    public List<SessionRecord> Sessions { get; set; } = [];
    public List<ProjectRecord> Projects { get; set; } = [];
    public List<BuildConfigRecord> Configs { get; set; } = [];
    public List<BuildJobRecord> Jobs { get; set; } = [];
    public List<BuildArtifactRecord> Artifacts { get; set; } = [];
    public List<AuditLogRecord> AuditLogs { get; set; } = [];
    public List<McpClientRecord> McpClients { get; set; } = [];
    public List<WorkerNodeRecord> Workers { get; set; } = [];
}

public sealed class UserRecord
{
    public string Id { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class SessionRecord
{
    public string TokenHash { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class ProjectRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RepositoryUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public List<string> AllowedBranches { get; set; } = ["main"];
    public string WorkspaceRoot { get; set; } = "~/UnityBuildWorkspace";
    public string ArtifactsRoot { get; set; } = "~/UnityBuildArtifacts";
    public int NextBuildNumber { get; set; } = 1;
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class BuildConfigRecord
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AllowMcpBuild { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class BuildJobRecord
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ConfigId { get; set; } = "";
    public string RequestedByUserId { get; set; } = "";
    public string Source { get; set; } = BuildSources.Web;
    public string Status { get; set; } = BuildStatuses.Queued;
    public string Branch { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public bool DryRun { get; set; }
    public bool SkipGit { get; set; }
    public bool SkipUnity { get; set; }
    public bool SkipXcode { get; set; }
    public bool AllowNonMac { get; set; }
    public string Notes { get; set; } = "";
    public string MaterializedConfigPath { get; set; } = "";
    public string WorkerLogPath { get; set; } = "";
    public string ArtifactRoot { get; set; } = "";
    public int? ExitCode { get; set; }
    public string Error { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class BuildArtifactRecord
{
    public string Id { get; set; } = "";
    public string JobId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class AuditLogRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class McpClientRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string UserId { get; set; } = "";
    public List<string> AllowedProjectIds { get; set; } = [];
    public bool CanStartBuild { get; set; } = true;
    public bool AllowFullBuild { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class WorkerNodeRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HostName { get; set; } = "";
    public string Status { get; set; } = WorkerStatuses.Offline;
    public string CurrentJobId { get; set; } = "";
    public List<string> UnityVersions { get; set; } = [];
    public List<string> XcodeVersions { get; set; } = [];
    public List<string> ProjectIds { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.Now;
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string ProjectOwner = "ProjectOwner";
    public const string Builder = "Builder";
    public const string Viewer = "Viewer";
    public const string Agent = "Agent";
}

public static class BuildStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";
}

public static class BuildSources
{
    public const string Web = "Web";
    public const string Mcp = "MCP";
}

public static class WorkerStatuses
{
    public const string Idle = "Idle";
    public const string Running = "Running";
    public const string Offline = "Offline";
}

public sealed record CurrentUser(string Id, string UserName, string DisplayName, string Role);

public sealed record LoginRequest(string UserName, string Password);

public sealed record ProjectRequest(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    string[]? AllowedBranches,
    string WorkspaceRoot,
    string ArtifactsRoot,
    string? Description);

public sealed record BuildConfigRequest(string ProjectId, string Name, string ConfigPath, bool AllowMcpBuild = false);

public sealed record StartBuildRequest(
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
