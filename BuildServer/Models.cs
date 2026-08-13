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
    public EmailSettingsRecord? EmailSettings { get; set; }
    public List<NotificationContactRecord> NotificationContacts { get; set; } = [];
    public List<ProjectProfileRecord> ProjectProfiles { get; set; } = [];
    public List<CertificateProfileRecord> CertificateProfiles { get; set; } = [];
    public List<SigningProfileRecord> SigningProfiles { get; set; } = [];
    public List<UnityProjectProfileRecord> UnityProjectProfiles { get; set; } = [];
    public AutomationToolSettingsRecord? AutomationToolSettings { get; set; }
}

public sealed class AutomationToolSettingsRecord
{
    public string Id { get; set; } = "automation-tool";
    public string Mode { get; set; } = "auto";
    public string ManualPath { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class NotificationContactRecord
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ProjectProfileRecord
{
    public string Id { get; set; } = "";
    public string ProjectRecordId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RepositoryUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public List<string> AllowedBranches { get; set; } = ["main"];
    public string DefaultBuildPlatform { get; set; } = BuildPlatforms.Ios;
    public string Description { get; set; } = "";
    public string ProjectDirectoryName { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "~/UnityBuildWorkspace";
    public string ArtifactsRoot { get; set; } = "~/UnityBuildArtifacts";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class UnityProjectProfileRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string UnityProjectRelativePath { get; set; } = ".";
    public string UnityVersion { get; set; } = "";
    public string UnityExecutablePath { get; set; } = "";
    public string UnityBuildMethod { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BundleIdentifier { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class CertificateProfileRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "ios";
    public string AppStoreConnectApiKeyPath { get; set; } = "";
    public string AppStoreConnectApiKeyId { get; set; } = "";
    public string AppStoreConnectApiIssuerId { get; set; } = "";
    public bool AppStoreConnectUploadEnabled { get; set; }
    public string AppStoreConnectUploadTarget { get; set; } = "testflight";
    public bool GooglePlayUploadEnabled { get; set; }
    public string GooglePlayPackageName { get; set; } = "";
    public string GooglePlayServiceAccountJsonPath { get; set; } = "";
    public string GooglePlayTrack { get; set; } = "internal";
    public string TiktokAppId { get; set; } = "";
    public string TiktokAccessToken { get; set; } = "";
    public string TiktokGameName { get; set; } = "";
    public string TiktokApiEndpoint { get; set; } = "https://open-api.tiktokglobalshop.com";
    public bool TiktokUploadEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class SigningProfileRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "ios";
    public string TeamId { get; set; } = "";
    public string ExportMethod { get; set; } = "development";
    public string SigningStyle { get; set; } = "automatic";
    public string IosDeploymentTarget { get; set; } = "";
    public string AndroidKeystoreName { get; set; } = "";
    public string AndroidKeystorePass { get; set; } = "";
    public string AndroidKeyaliasName { get; set; } = "";
    public string AndroidKeyaliasPass { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class UserRecord
{
    public string Id { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public List<string> AllowedProjectIds { get; set; } = [];
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
    public string DefaultBuildPlatform { get; set; } = BuildPlatforms.Ios;
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class BuildConfigRecord
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string BuildPlatform { get; set; } = BuildPlatforms.Ios;
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
    public string BuildPlatform { get; set; } = BuildPlatforms.Ios;
    public string Branch { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public bool DryRun { get; set; }
    public bool SkipGit { get; set; }
    public bool SkipUnity { get; set; }
    public bool SkipXcode { get; set; }
    public bool AllowNonMac { get; set; }
    public string ClientRequestId { get; set; } = "";
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

    public long LogPushOffset { get; set; }
    public string GatewayCommandId { get; set; } = "";
    public List<string> NotifyEmails { get; set; } = [];
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
    public const string Gateway = "Gateway";
}

public static class WorkerStatuses
{
    public const string Idle = "Idle";
    public const string Running = "Running";
    public const string Offline = "Offline";
}

public sealed record CurrentUser(
    string Id,
    string UserName,
    string DisplayName,
    string Role,
    IReadOnlyList<string>? AllowedProjectIds = null);

public sealed record LoginRequest(string UserName, string Password);

public sealed record UserRequest(
    string UserName,
    string DisplayName,
    string Role,
    string? Password,
    bool Enabled = true,
    string[]? AllowedProjectIds = null);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ProjectRequest(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    string[]? AllowedBranches,
    string WorkspaceRoot,
    string ArtifactsRoot,
    string? DefaultBuildPlatform,
    string? Description);

public sealed record BuildConfigRequest(string ProjectId, string Name, string ConfigPath, string? BuildPlatform = null, bool AllowMcpBuild = false);

public sealed record BuildConfigFileRequest(
    string ProjectId,
    string Name,
    string? BuildPlatform,
    string? FileName,
    string? ProjectDirectoryName,
    string? UnityProjectRelativePath,
    string? UnityVersion,
    string? UnityExecutablePath,
    string? UnityBuildMethod,
    string? ProductName,
    string? BundleIdentifier,
    string? BundleVersion,
    bool SyncBundleVersionFromUnity = true,
    string? BuildNumber = null,
    bool AutoIncrementBuildNumber = true,
    string? IosDeploymentTarget = "13.0",
    string? TeamId = null,
    string? SigningStyle = "automatic",
    string? ExportMethod = "development",
    bool AllowProvisioningUpdates = true,
    bool CopyArchiveToOrganizer = true,
    bool AppStoreConnectUploadEnabled = false,
    string? AppStoreConnectUploadTarget = "testflight",
    string? AppStoreConnectApiKeyPath = null,
    string? AppStoreConnectApiKeyId = null,
    string? AppStoreConnectApiIssuerId = null,
    string? AndroidBuildFormat = "aab",
    string? AndroidOutputDirectory = null,
    string? ApkOutputPath = null,
    string? AabOutputPath = null,
    string? AndroidMinSdkVersion = null,
    string? AndroidTargetSdkVersion = null,
    string? AndroidKeystoreName = null,
    string? AndroidKeystorePass = null,
    string? AndroidKeyaliasName = null,
    string? AndroidKeyaliasPass = null,
    bool GooglePlayUploadEnabled = false,
    string? GooglePlayPackageName = null,
    string? GooglePlayServiceAccountJsonPath = null,
    string? GooglePlayTrack = "internal",
    string? GooglePlayReleaseStatus = "draft",
    string? GooglePlayReleaseName = null,
    string? GooglePlayUploadArtifact = "aab",
    bool GooglePlayChangesNotSentForReview = false,
    double? GooglePlayUserFraction = null,
    bool AllowMcpBuild = false,
    bool OverwriteExisting = false,
    string? TiktokAppId = null,
    string? TiktokAccessToken = null,
    string? TiktokGameName = null,
    string? TiktokWebglOutputDirectory = null,
    bool TiktokUploadEnabled = false,
    string? TiktokApiEndpoint = null);

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
    string? ClientRequestId = null,
    string? Notes = null,
    string[]? NotifyEmails = null);

public sealed class EmailSettingsRecord
{
    public string Id { get; set; } = "email-settings";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUserName { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
    public bool UseSsl { get; set; } = true;
    public bool Enabled { get; set; } = false;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed record EmailSettingsRequest(
    string SmtpHost,
    int SmtpPort,
    string SmtpUserName,
    string? SmtpPassword,
    string FromEmail,
    string? FromName,
    bool UseSsl,
    bool Enabled);

public sealed record TestEmailRequest(string ToEmail);

public sealed record NotificationContactRequest(string Title, string Email, bool Enabled = true);

public sealed record ProjectProfileRequest(
    string Name,
    string? RepositoryUrl = null,
    string? DefaultBranch = null,
    string[]? AllowedBranches = null,
    string? DefaultBuildPlatform = null,
    string? Description = null,
    string? ProjectDirectoryName = null,
    string? WorkspaceRoot = null,
    string? ArtifactsRoot = null);

public sealed record UnityProjectProfileRequest(
    string Name,
    string? UnityProjectRelativePath = null,
    string? UnityVersion = null,
    string? UnityExecutablePath = null,
    string? UnityBuildMethod = null,
    string? ProductName = null,
    string? BundleIdentifier = null);

public sealed record CertificateProfileRequest(
    string Name,
    string? Platform = "ios",
    string? AppStoreConnectApiKeyPath = null,
    string? AppStoreConnectApiKeyId = null,
    string? AppStoreConnectApiIssuerId = null,
    bool AppStoreConnectUploadEnabled = false,
    string? AppStoreConnectUploadTarget = "testflight",
    bool GooglePlayUploadEnabled = false,
    string? GooglePlayPackageName = null,
    string? GooglePlayServiceAccountJsonPath = null,
    string? GooglePlayTrack = "internal",
    string? TiktokAppId = null,
    string? TiktokAccessToken = null,
    string? TiktokGameName = null,
    string? TiktokApiEndpoint = "https://open-api.tiktokglobalshop.com",
    bool TiktokUploadEnabled = false);

public sealed record SigningProfileRequest(
    string Name,
    string? Platform = "ios",
    string? TeamId = null,
    string? ExportMethod = "development",
    string? SigningStyle = "automatic",
    string? IosDeploymentTarget = null,
    string? AndroidKeystoreName = null,
    string? AndroidKeystorePass = null,
    string? AndroidKeyaliasName = null,
    string? AndroidKeyaliasPass = null);

public sealed record BatchDeleteRequest(string[] JobIds);

public sealed record AutomationToolRequest(string Mode, string? ManualPath = null);

public static class BuildPlatforms
{
    public const string Ios = "ios";
    public const string Android = "android";
    public const string Tiktok = "tiktok";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Ios, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Android, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Tiktok, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Ios : value.Trim().ToLowerInvariant();
    }
}

public static class AndroidBuildFormats
{
    public const string Apk = "apk";
    public const string Aab = "aab";
    public const string Both = "both";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Apk, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Aab, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Both, StringComparison.OrdinalIgnoreCase);
    }
}

public static class DefaultUnityBuildMethods
{
    public const string Ios = "BuildAutomation.IOSBuilder.Build";
    public const string Android = "BuildAutomation.AndroidBuilder.Build";
    public const string Tiktok = "BuildAutomation.TiktokBuilder.Build";
}
