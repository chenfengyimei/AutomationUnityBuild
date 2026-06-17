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
    public static class AndroidBuilder
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
            AndroidCommandLineArguments args = AndroidCommandLineArguments.Parse(Environment.GetCommandLineArgs());
            string buildFormat = args.Get("-customAndroidBuildFormat", "aab").Trim().ToLowerInvariant();
            string apkPath = args.Get("-customApkPath", "Builds/Android/game.apk");
            string aabPath = args.Get("-customAabPath", "Builds/Android/game.aab");
            string buildNumber = args.Get("-customBuildNumber", "");
            string bundleVersion = args.Get("-customBundleVersion", "");
            string bundleIdentifier = args.Get("-customBundleIdentifier", "");
            string productName = args.Get("-customProductName", "");
            string minSdkVersion = args.Get("-customAndroidMinSdkVersion", "");
            string targetSdkVersion = args.Get("-customAndroidTargetSdkVersion", "");
            string keystoreName = args.Get("-customAndroidKeystoreName", "");
            string keystorePass = args.Get("-customAndroidKeystorePass", "");
            string keyaliasName = args.Get("-customAndroidKeyaliasName", "");
            string keyaliasPass = args.Get("-customAndroidKeyaliasPass", "");
            string metadataPath = args.Get("-customBuildMetadataPath", "");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("切换到 Android BuildTarget 失败，请确认当前 Unity 安装了 Android Build Support。");
            }

            ApplyPlayerSettings(
                bundleIdentifier,
                productName,
                bundleVersion,
                buildNumber,
                minSdkVersion,
                targetSdkVersion,
                keystoreName,
                keystorePass,
                keyaliasName,
                keyaliasPass);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings 里没有启用任何 Scene。");
            }

            string builtApkPath = "";
            string builtAabPath = "";
            if (buildFormat == "apk" || buildFormat == "both")
            {
                builtApkPath = BuildArtifact(scenes, apkPath, buildAppBundle: false);
            }

            if (buildFormat == "aab" || buildFormat == "both")
            {
                builtAabPath = BuildArtifact(scenes, aabPath, buildAppBundle: true);
            }

            if (string.IsNullOrWhiteSpace(builtApkPath) && string.IsNullOrWhiteSpace(builtAabPath))
            {
                throw new InvalidOperationException("customAndroidBuildFormat 必须是 apk、aab 或 both。");
            }

            WriteBuildMetadata(metadataPath, builtApkPath, builtAabPath);
        }

        private static string BuildArtifact(string[] scenes, string outputPath, bool buildAppBundle)
        {
            string directory = Path.GetDirectoryName(outputPath) ?? "";
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            EditorUserBuildSettings.buildAppBundle = buildAppBundle;
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Unity Android 构建失败: {summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException($"BuildPipeline 返回成功，但没有找到 Android 产物: {outputPath}");
            }

            Debug.Log($"Unity Android 构建完成: {outputPath}, size={summary.totalSize} bytes");
            return outputPath;
        }

        private static void ApplyPlayerSettings(
            string bundleIdentifier,
            string productName,
            string bundleVersion,
            string buildNumber,
            string minSdkVersion,
            string targetSdkVersion,
            string keystoreName,
            string keystorePass,
            string keyaliasName,
            string keyaliasPass)
        {
            if (!string.IsNullOrWhiteSpace(bundleIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, bundleIdentifier);
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
                if (!int.TryParse(buildNumber, out int versionCode) || versionCode <= 0)
                {
                    throw new InvalidOperationException("Android Version Code 必须是大于 0 的整数。");
                }

                PlayerSettings.Android.bundleVersionCode = versionCode;
            }

            if (int.TryParse(minSdkVersion, out int minSdk))
            {
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minSdk;
            }

            if (int.TryParse(targetSdkVersion, out int targetSdk))
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetSdk;
            }

            if (!string.IsNullOrWhiteSpace(keystoreName))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystoreName;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyaliasName;
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
            }
        }

        private static void WriteBuildMetadata(string metadataPath, string apkPath, string aabPath)
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
                buildNumber = PlayerSettings.Android.bundleVersionCode.ToString(),
                bundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
                productName = PlayerSettings.productName,
                androidMinSdkVersion = ((int)PlayerSettings.Android.minSdkVersion).ToString(),
                androidTargetSdkVersion = ((int)PlayerSettings.Android.targetSdkVersion).ToString(),
                apkPath = apkPath,
                aabPath = aabPath
            };

            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
            Debug.Log($"Unity Android 构建元数据已写出: {metadataPath}");
        }

        [Serializable]
        private sealed class BuildMetadata
        {
            public string bundleVersion;
            public string buildNumber;
            public string bundleIdentifier;
            public string productName;
            public string androidMinSdkVersion;
            public string androidTargetSdkVersion;
            public string apkPath;
            public string aabPath;
        }
    }

    internal sealed class AndroidCommandLineArguments
    {
        private readonly Dictionary<string, string> _values;

        private AndroidCommandLineArguments(Dictionary<string, string> values)
        {
            _values = values;
        }

        public static AndroidCommandLineArguments Parse(string[] args)
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

            return new AndroidCommandLineArguments(values);
        }

        public string Get(string key, string fallback)
        {
            return _values.TryGetValue(key, out string value) ? value : fallback;
        }
    }
}
#endif
