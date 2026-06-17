using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class UnityBuildMetadataReader(BuildRunContext context, string unityEditorScriptName)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public void SyncBundleVersionFromUnityMetadata()
    {
        if (!_config.SyncBundleVersionFromUnity || _options.SkipUnity)
        {
            return;
        }

        if (!File.Exists(_paths.UnityBuildMetadataPath))
        {
            _logger.Warn($"已开启 Bundle Version 同步，但没有找到 Unity 构建元数据: {_paths.UnityBuildMetadataPath}");
            _logger.Warn($"请确认 Unity 项目里的 Assets/Editor/{unityEditorScriptName} 已更新到当前工具版本。");
            return;
        }

        UnityBuildMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<UnityBuildMetadata>(
                File.ReadAllText(_paths.UnityBuildMetadataPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Warn($"读取 Unity 构建元数据失败，跳过 Bundle Version 同步: {ex.Message}");
            return;
        }

        string unityBundleVersion = metadata?.BundleVersion?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(unityBundleVersion))
        {
            _logger.Warn("Unity 构建元数据里没有 bundleVersion，跳过 Bundle Version 同步。");
            return;
        }

        if (string.Equals(_config.BundleVersion, unityBundleVersion, StringComparison.Ordinal))
        {
            _logger.Info($"Bundle Version 已与 Unity 项目一致: {unityBundleVersion}");
            return;
        }

        _logger.Info($"同步 Unity 项目 Bundle Version: {BuildDisplay.BundleVersion(_config.BundleVersion)} -> {unityBundleVersion}");
        _config.BundleVersion = unityBundleVersion;
        context.MarkRuntimeConfigChanged();
    }

    private sealed class UnityBuildMetadata
    {
        [JsonPropertyName("bundleVersion")]
        public string? BundleVersion { get; set; }
    }
}
