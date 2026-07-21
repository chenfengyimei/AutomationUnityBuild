#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildAutomation
{
    public static class TiktokBuilder
    {
        public static void Build()
        {
            try
            {
                BuildInternal();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildInternal()
        {
            TiktokCommandLineArguments args = TiktokCommandLineArguments.Parse(Environment.GetCommandLineArgs());
            string webglOutputPath = args.Get("-customWebglOutputPath", "Builds/WebGL");
            string buildNumber = args.Get("-customBuildNumber", "");
            string bundleVersion = args.Get("-customBundleVersion", "");
            string bundleIdentifier = args.Get("-customBundleIdentifier", "");
            string productName = args.Get("-customProductName", "");
            string metadataPath = args.Get("-customBuildMetadataPath", "");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException(
                    "切换到 WebGL BuildTarget 失败。请确认当前 Unity 安装了 WebGL Build Support 模块。");
            }

            ApplyPlayerSettings(bundleIdentifier, productName, bundleVersion, buildNumber);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings 里没有启用任何 Scene。");
            }

            if (Directory.Exists(webglOutputPath))
            {
                Directory.Delete(webglOutputPath, recursive: true);
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = webglOutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unity WebGL 构建失败: {summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            if (!Directory.Exists(webglOutputPath))
            {
                throw new DirectoryNotFoundException(
                    $"BuildPipeline 返回成功，但没有找到 WebGL 输出目录: {webglOutputPath}");
            }

            Debug.Log($"Unity WebGL 构建完成: {webglOutputPath}, size={summary.totalSize} bytes");
            WriteBuildMetadata(metadataPath, webglOutputPath);
        }

        private static void ApplyPlayerSettings(
            string bundleIdentifier,
            string productName,
            string bundleVersion,
            string buildNumber)
        {
            if (!string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, bundleIdentifier);
            }

            if (!string.IsNullOrWhiteSpace(productName))
            {
                PlayerSettings.productName = productName;
            }

            if (!string.IsNullOrWhiteSpace(bundleVersion))
            {
                PlayerSettings.bundleVersion = bundleVersion;
            }

            PlayerSettings.WebGL.codeOptimization = WebGLCodeOptimization.RuntimeSpeed;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
        }

        private static void WriteBuildMetadata(string metadataPath, string webglOutputPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(metadataPath) ?? "";
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var metadata = new BuildMetadata
            {
                bundleVersion = PlayerSettings.bundleVersion,
                bundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.WebGL),
                productName = PlayerSettings.productName,
                webglOutputPath = webglOutputPath
            };

            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
            Debug.Log($"Unity WebGL 构建元数据已写出: {metadataPath}");
        }

        [Serializable]
        private sealed class BuildMetadata
        {
            public string bundleVersion;
            public string bundleIdentifier;
            public string productName;
            public string webglOutputPath;
        }
    }

    internal sealed class TiktokCommandLineArguments
    {
        private readonly Dictionary<string, string> _values;

        private TiktokCommandLineArguments(Dictionary<string, string> values)
        {
            _values = values;
        }

        public static TiktokCommandLineArguments Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    values[key] = args[i + 1];
                    i++;
                }
                else
                {
                    values[key] = "true";
                }
            }

            return new TiktokCommandLineArguments(values);
        }

        public string Get(string key, string fallback)
        {
            return _values.TryGetValue(key, out string value) ? value : fallback;
        }
    }
}
#endif