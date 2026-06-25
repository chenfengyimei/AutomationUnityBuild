using System.Text.RegularExpressions;
using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class ArtifactScanner(JsonDatabase database, BuildServerOptions options)
{
    private static readonly Regex ArtifactRootRegex = new(@"产物目录:\s*(?<path>.+)$", RegexOptions.Compiled);
    private static readonly EnumerationOptions ArtifactEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public async Task ScanAsync(BuildJobRecord job)
    {
        string artifactRoot = FindArtifactRoot(job);
        if (string.IsNullOrWhiteSpace(artifactRoot) || !Directory.Exists(artifactRoot))
        {
            return;
        }

        artifactRoot = Path.GetFullPath(artifactRoot);
        if (!IsAllowedArtifactRoot(artifactRoot))
        {
            return;
        }

        List<BuildArtifactRecord> artifacts = [];
        foreach (string path in Directory.EnumerateFileSystemEntries(artifactRoot, "*", ArtifactEnumerationOptions))
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
            ".apk" => "apk",
            ".aab" => "aab",
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

        return Directory.EnumerateFiles(path, "*", ArtifactEnumerationOptions)
            .Sum(file => new FileInfo(file).Length);
    }

    private bool IsAllowedArtifactRoot(string path)
    {
        return options.AllowedArtifactsRoots.Count == 0 ||
               options.AllowedArtifactsRoots.Any(root => IsSameOrChild(path, root));
    }

    private static bool IsSameOrChild(string path, string root)
    {
        string normalizedPath = NormalizeDirectory(path);
        string normalizedRoot = NormalizeDirectory(root);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedPath.Equals(normalizedRoot, comparison) ||
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }
}
