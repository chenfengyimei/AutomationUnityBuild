using System.Text.RegularExpressions;
using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class ArtifactScanner(JsonDatabase database)
{
    private static readonly Regex ArtifactRootRegex = new(@"产物目录:\s*(?<path>.+)$", RegexOptions.Compiled);

    public async Task ScanAsync(BuildJobRecord job)
    {
        string artifactRoot = FindArtifactRoot(job);
        if (string.IsNullOrWhiteSpace(artifactRoot) || !Directory.Exists(artifactRoot))
        {
            return;
        }

        List<BuildArtifactRecord> artifacts = [];
        foreach (string path in Directory.EnumerateFileSystemEntries(artifactRoot, "*", SearchOption.AllDirectories))
        {
            string type = ArtifactType(path);
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            artifacts.Add(new BuildArtifactRecord
            {
                Id = Ids.New("art"),
                JobId = job.Id,
                Type = type,
                Path = path,
                SizeBytes = SizeOf(path),
                CreatedAt = DateTimeOffset.Now
            });
        }

        await database.UpdateAsync(db =>
        {
            BuildJobRecord? storedJob = db.Jobs.FirstOrDefault(item => item.Id == job.Id);
            if (storedJob is not null)
            {
                storedJob.ArtifactRoot = artifactRoot;
            }

            db.Artifacts.RemoveAll(item => item.JobId == job.Id);
            db.Artifacts.AddRange(artifacts);
        });
    }

    private static string FindArtifactRoot(BuildJobRecord job)
    {
        if (!File.Exists(job.WorkerLogPath))
        {
            return "";
        }

        foreach (string line in File.ReadLines(job.WorkerLogPath).Reverse())
        {
            Match match = ArtifactRootRegex.Match(line);
            if (match.Success)
            {
                return match.Groups["path"].Value.Trim();
            }
        }

        return "";
    }

    private static string ArtifactType(string path)
    {
        if (Directory.Exists(path) && path.EndsWith(".xcarchive", StringComparison.OrdinalIgnoreCase))
        {
            return "xcarchive";
        }

        if (!File.Exists(path))
        {
            return "";
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ipa" => "ipa",
            ".log" => "log",
            ".json" => "json",
            ".plist" => "plist",
            _ => ""
        };
    }

    private static long SizeOf(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path).Length;
        }

        if (!Directory.Exists(path))
        {
            return 0;
        }

        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }
}
