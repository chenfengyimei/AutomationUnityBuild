namespace AutomationUnityBuildIOS;

internal sealed class GitRepositoryPolicyValidator(BuildRunContext context)
{
    public void Validate()
    {
        GitRepositoryPolicy.Validate(context.Config, context.Logger);
    }
}

internal static class GitRepositoryPolicy
{
    public static void Validate(BuildConfig config, BuildLogger logger)
    {
        ValidateRepositoryUrlFormat(config.RepositoryUrl);

        if (config.RepositoryUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            logger.Info("GitHub HTTPS 地址不会支持账号密码登录。公开仓库可直接 clone；私有仓库建议改用 SSH 地址 git@github.com:owner/repo.git。");
        }

        if (config.AllowedRepositoryUrls.Count == 0)
        {
            logger.Info("Git 仓库白名单未配置，将只校验仓库地址格式。");
            return;
        }

        string repositoryKey = GitRepositoryUrl.CanonicalKey(config.RepositoryUrl);
        bool isAllowed = config.AllowedRepositoryUrls
            .Select(GitRepositoryUrl.CanonicalKey)
            .Any(allowedKey => string.Equals(allowedKey, repositoryKey, StringComparison.OrdinalIgnoreCase));
        if (isAllowed)
        {
            logger.Info("Git 仓库白名单校验通过。");
            return;
        }

        string allowed = string.Join(Environment.NewLine, config.AllowedRepositoryUrls.Select(url => $"  - {GitRepositoryUrl.Redact(url)}"));
        throw new InvalidOperationException(
            $"Git 仓库不在 allowedRepositoryUrls 白名单内，已停止打包。{Environment.NewLine}" +
            $"配置仓库: {GitRepositoryUrl.Redact(config.RepositoryUrl)}{Environment.NewLine}" +
            $"允许仓库:{Environment.NewLine}{allowed}");
    }

    private static void ValidateRepositoryUrlFormat(string repositoryUrl)
    {
        if (repositoryUrl.Any(char.IsWhiteSpace) ||
            repositoryUrl.Contains('[') ||
            repositoryUrl.Contains(']'))
        {
            throw new InvalidOperationException(
                $"Git 仓库地址格式不正确: {GitRepositoryUrl.Redact(repositoryUrl)}{Environment.NewLine}" +
                "请填写 git clone 可直接使用的地址，例如 https://github.com/owner/repo.git 或 git@github.com:owner/repo.git。");
        }
    }
}
