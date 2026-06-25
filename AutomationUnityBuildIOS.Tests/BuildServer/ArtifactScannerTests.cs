using BuildServer;
using BuildServer.Persistence;
using BuildServer.Services;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class ArtifactScannerTests
{
    [Fact]
    public async Task ScanAsync_DoesNotPersistArtifacts_OutsideAllowedArtifactRoots()
    {
        string dataRoot = TestHelpers.CreateTempDir();
        string allowedRoot = TestHelpers.CreateTempDir();
        string outsideRoot = TestHelpers.CreateTempDir();

        try
        {
            string artifactPath = Path.Combine(outsideRoot, "release.apk");
            await File.WriteAllTextAsync(artifactPath, "fake apk");

            string logPath = Path.Combine(dataRoot, "worker.log");
            await File.WriteAllTextAsync(logPath, $"产物目录: {outsideRoot}{Environment.NewLine}");

            var options = new BuildServerOptions
            {
                DataRoot = dataRoot,
                AllowedArtifactsRoots = [allowedRoot]
            };
            var database = new JsonDatabase(options);
            await database.InitializeAsync();

            var job = new BuildJobRecord
            {
                Id = "job-1",
                ProjectId = "project-1",
                ConfigId = "config-1",
                WorkerLogPath = logPath
            };
            await database.UpdateAsync(db => db.Jobs.Add(job));

            var scanner = new ArtifactScanner(database, options);
            await scanner.ScanAsync(job);

            (string artifactRoot, int artifactCount) = await database.ReadAsync(db =>
            {
                BuildJobRecord stored = db.Jobs.Single(item => item.Id == job.Id);
                return (stored.ArtifactRoot, db.Artifacts.Count(item => item.JobId == job.Id));
            });

            Assert.Equal("", artifactRoot);
            Assert.Equal(0, artifactCount);
        }
        finally
        {
            TestHelpers.CleanupTempDir(dataRoot);
            TestHelpers.CleanupTempDir(allowedRoot);
            TestHelpers.CleanupTempDir(outsideRoot);
        }
    }
}
