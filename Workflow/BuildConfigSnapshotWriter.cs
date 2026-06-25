using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutomationUnityBuildIOS;

internal sealed class BuildConfigSnapshotWriter(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public void Write()
    {
        if (!_config.SaveConfigSnapshot)
        {
            _logger.Info("配置快照已关闭: saveConfigSnapshot=false");
            return;
        }

        if (_options.DryRun)
        {
            _logger.Info($"[dry-run] 生成配置快照: {_paths.ConfigSnapshotPath}");
        }

        PathTools.EnsureParentDirectory(_paths.ConfigSnapshotPath);
        JsonObject snapshot = CreateSnapshot();
        File.WriteAllText(
            _paths.ConfigSnapshotPath,
            snapshot.ToJsonString(JsonOptions.IndentedCamelCase) + Environment.NewLine,
            TextEncodings.Utf8Bom);
        _logger.Info($"已生成配置快照: {_paths.ConfigSnapshotPath}");
    }

    private JsonObject CreateSnapshot()
    {
        JsonNode? configNode = JsonSerializer.SerializeToNode(_config, JsonOptions.IndentedCamelCase);
        RedactNode(configNode);

        JsonNode? pathsNode = JsonSerializer.SerializeToNode(new
        {
            _paths.RunId,
            _paths.WorkspaceRoot,
            _paths.RepositoryRoot,
            _paths.UnityProjectRoot,
            _paths.UnityExecutable,
            _paths.ArtifactsRoot,
            _paths.ArtifactsRunRoot,
            _paths.XcodeOutputDirectory,
            _paths.ArchivePath,
            _paths.ExportPath,
            _paths.LogsDirectory,
            _paths.AutomationLogPath,
            _paths.UnityLogPath,
            _paths.UnityProcessLogPath,
            _paths.UnityBuildMetadataPath,
            _paths.ConfigSnapshotPath,
            _paths.XcodeArchiveLogPath,
            _paths.XcodeExportLogPath,
            _paths.ExportOptionsPlistPath,
            _paths.AndroidOutputDirectory,
            _paths.ApkOutputPath,
            _paths.AabOutputPath
        }, JsonOptions.IndentedCamelCase);
        RedactNode(pathsNode);

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdAt"] = DateTimeOffset.Now.ToString("O"),
            ["runId"] = _paths.RunId,
            ["configName"] = _config.ConfigName,
            ["sourceConfigPath"] = Path.GetFullPath(_options.ConfigPath),
            ["options"] = JsonSerializer.SerializeToNode(new
            {
                _options.DryRun,
                _options.SkipGit,
                _options.SkipUnity,
                _options.SkipXcode,
                _options.AllowNonMac,
                _options.Verbose
            }, JsonOptions.IndentedCamelCase),
            ["config"] = configNode,
            ["paths"] = pathsNode
        };
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (string key in jsonObject.Select(pair => pair.Key).ToArray())
                {
                    JsonNode? child = jsonObject[key];
                    if (IsSensitiveKey(key))
                    {
                        if (child is JsonValue secretValue &&
                            secretValue.TryGetValue(out string? secretString) &&
                            string.IsNullOrEmpty(secretString))
                        {
                            jsonObject[key] = "";
                        }
                        else
                        {
                            jsonObject[key] = "***";
                        }

                        continue;
                    }

                    if (child is JsonValue value &&
                        value.TryGetValue(out string? stringValue) &&
                        stringValue is not null)
                    {
                        jsonObject[key] = SensitiveText.Redact(stringValue);
                        continue;
                    }

                    RedactNode(child);
                }
                break;

            case JsonArray jsonArray:
                for (int i = 0; i < jsonArray.Count; i++)
                {
                    JsonNode? child = jsonArray[i];
                    if (child is JsonValue value &&
                        value.TryGetValue(out string? stringValue) &&
                        stringValue is not null)
                    {
                        jsonArray[i] = SensitiveText.Redact(stringValue);
                        continue;
                    }

                    RedactNode(child);
                }
                break;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        string normalized = key.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("passwd", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("keystorepass", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("keyaliaspass", StringComparison.OrdinalIgnoreCase);
    }
}
