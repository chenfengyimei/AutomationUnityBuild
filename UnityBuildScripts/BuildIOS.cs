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
    public static class IOSBuilder
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
            CommandLineArguments args = CommandLineArguments.Parse(Environment.GetCommandLineArgs());
            string outputPath = args.Get("-customBuildPath", "Builds/iOS");
            string buildNumber = args.Get("-customBuildNumber", "");
            string bundleVersion = args.Get("-customBundleVersion", "");
            string bundleIdentifier = args.Get("-customBundleIdentifier", "");
            string productName = args.Get("-customProductName", "");
            string appleTeamId = args.Get("-customAppleTeamId", "");
            string iosDeploymentTarget = args.Get("-customIosDeploymentTarget", "");

            Directory.CreateDirectory(outputPath);

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                throw new InvalidOperationException("切换到 iOS BuildTarget 失败，请确认当前 Unity 安装了 iOS Build Support。");
            }

            ApplyPlayerSettings(bundleIdentifier, productName, bundleVersion, buildNumber, appleTeamId, iosDeploymentTarget);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings 里没有启用任何 Scene。");
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Unity iOS 导出失败: {summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            string xcodeProjectPath = Path.Combine(outputPath, "Unity-iPhone.xcodeproj");
            if (!Directory.Exists(xcodeProjectPath))
            {
                throw new DirectoryNotFoundException($"BuildPipeline 返回成功，但没有找到 Xcode 工程: {xcodeProjectPath}");
            }

            Debug.Log($"Unity iOS 导出完成: {outputPath}, size={summary.totalSize} bytes");
        }

        private static void ApplyPlayerSettings(
            string bundleIdentifier,
            string productName,
            string bundleVersion,
            string buildNumber,
            string appleTeamId,
            string iosDeploymentTarget)
        {
            if (!string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, bundleIdentifier);
            }

            if (!string.IsNullOrWhiteSpace(productName))
            {
                PlayerSettings.productName = productName;
            }

            if (!string.IsNullOrWhiteSpace(bundleVersion))
            {
                PlayerSettings.bundleVersion = bundleVersion;
            }

            if (!string.IsNullOrWhiteSpace(buildNumber))
            {
                PlayerSettings.iOS.buildNumber = buildNumber;
            }

            if (!string.IsNullOrWhiteSpace(appleTeamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = appleTeamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }

            if (!string.IsNullOrWhiteSpace(iosDeploymentTarget))
            {
                PlayerSettings.iOS.targetOSVersionString = iosDeploymentTarget;
            }
        }
    }

    internal sealed class CommandLineArguments
    {
        private readonly Dictionary<string, string> _values;

        private CommandLineArguments(Dictionary<string, string> values)
        {
            _values = values;
        }

        public static CommandLineArguments Parse(string[] args)
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

            return new CommandLineArguments(values);
        }

        public string Get(string key, string fallback)
        {
            return _values.TryGetValue(key, out string value) ? value : fallback;
        }
    }
}
#endif
