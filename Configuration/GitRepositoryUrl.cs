namespace AutomationUnityBuildIOS;

internal static class GitRepositoryUrl
{
    public static string CanonicalKey(string url)
    {
        string normalized = ConfigValueNormalizer.NormalizeRepositoryUrl(url).Trim().TrimEnd('/', '\\');
        if (normalized.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            int atIndex = normalized.IndexOf('@');
            int colonIndex = normalized.IndexOf(':', atIndex + 1);
            if (atIndex >= 0 && colonIndex > atIndex)
            {
                string host = normalized[(atIndex + 1)..colonIndex];
                string path = normalized[(colonIndex + 1)..];
                return TrimGitSuffix($"{host}/{path}").ToLowerInvariant();
            }
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
        {
            return TrimGitSuffix($"{uri.Host}{uri.AbsolutePath}").TrimEnd('/').ToLowerInvariant();
        }

        return TrimGitSuffix(normalized).ToLowerInvariant();
    }

    public static string Redact(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return url;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = "***",
            Password = ""
        };
        return builder.Uri.ToString();
    }

    private static string TrimGitSuffix(string value)
    {
        return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }
}
