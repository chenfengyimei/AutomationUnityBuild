using System.Text.RegularExpressions;

namespace AutomationUnityBuildIOS;

internal static class ConfigValueNormalizer
{
    private static readonly Regex MarkdownLinkRegex = new(@"\((?<url>https?://[^)\s]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BareGitHubRegex = new(@"^https?://github\.com/[^/\s]+/[^/\s#?]+/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeRepositoryUrl(string value)
    {
        string normalized = (value ?? "").Trim();
        Match markdownMatch = MarkdownLinkRegex.Match(normalized);
        if (markdownMatch.Success)
        {
            normalized = markdownMatch.Groups["url"].Value;
        }

        int queryIndex = normalized.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        normalized = normalized.Trim().TrimEnd('/');

        if (BareGitHubRegex.IsMatch(normalized) && !normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".git";
        }

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".git";
        }

        return normalized;
    }
}

