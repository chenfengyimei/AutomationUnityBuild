namespace AutomationUnityBuildIOS;

internal static class SampleFiles
{
    public const string BuildIosConfigJson =
        """
        {
          "configName": "dev",
          "buildPlatform": "ios",
          "repositoryUrl": "git@github.com:your-org/your-unity-game.git",
          "allowedRepositoryUrls": [
            "git@github.com:your-org/your-unity-game.git"
          ],
          "branch": "main",
          "workspaceRoot": "~/UnityBuildWorkspace",
          "allowedWorkspaceRoots": [
            "~/UnityBuildWorkspace"
          ],
          "projectDirectoryName": "YourUnityGame",
          "unityProjectRelativePath": ".",

          "unityVersion": "6000.0.0f1",
          "unityExecutablePath": "",
          "unityBuildMethod": "BuildAutomation.IOSBuilder.Build",

          "artifactsRoot": "~/UnityBuildArtifacts/YourUnityGame",
          "allowedArtifactsRoots": [
            "~/UnityBuildArtifacts/YourUnityGame"
          ],
          "xcodeOutputDirectory": "",
          "archivePath": "",
          "exportPath": "",
          "logsDirectory": "",

          "scheme": "Unity-iPhone",
          "configuration": "Release",
          "exportMethod": "development",
          "teamId": "ABCDE12345",
          "signingStyle": "automatic",
          "exportOptionsPlistPath": "",

          "bundleIdentifier": "com.company.game",
          "productName": "Your Game",
          "bundleVersion": "1.0.0",
          "syncBundleVersionFromUnity": true,
          "buildNumber": "1",
          "autoIncrementBuildNumber": true,
          "iosDeploymentTarget": "13.0",

          "allowProvisioningUpdates": true,
          "resetRepository": false,
          "preserveUnityLibraryOnReset": true,
          "cleanXcodeOutputBeforeBuild": true,
          "useWorkspaceIfPresent": true,
          "generateExportOptionsPlist": true,
          "copyArchiveToOrganizer": true,
          "saveConfigSnapshot": true,
          "compileBitcode": null,
          "uploadSymbols": true,

          "xcodeBuildSettings": {},
          "environment": {},
          "provisioningProfiles": {}
        }
        """;

    public const string BuildAndroidConfigJson =
        """
        {
          "configName": "android-release",
          "buildPlatform": "android",
          "repositoryUrl": "git@github.com:your-org/your-unity-game.git",
          "allowedRepositoryUrls": [
            "git@github.com:your-org/your-unity-game.git"
          ],
          "branch": "main",
          "workspaceRoot": "~/UnityBuildWorkspace",
          "allowedWorkspaceRoots": [
            "~/UnityBuildWorkspace"
          ],
          "projectDirectoryName": "YourUnityGame",
          "unityProjectRelativePath": ".",

          "unityVersion": "6000.0.0f1",
          "unityExecutablePath": "",
          "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",

          "artifactsRoot": "~/UnityBuildArtifacts/YourUnityGame-Android",
          "allowedArtifactsRoots": [
            "~/UnityBuildArtifacts/YourUnityGame-Android"
          ],
          "logsDirectory": "",

          "bundleIdentifier": "com.company.game",
          "productName": "Your Game",
          "bundleVersion": "1.0.0",
          "syncBundleVersionFromUnity": true,
          "buildNumber": "1",
          "autoIncrementBuildNumber": true,

          "androidBuildFormat": "both",
          "androidOutputDirectory": "",
          "apkOutputPath": "",
          "aabOutputPath": "",
          "androidMinSdkVersion": "",
          "androidTargetSdkVersion": "",
          "androidKeystoreName": "",
          "androidKeystorePass": "",
          "androidKeyaliasName": "",
          "androidKeyaliasPass": "",

          "googlePlayUploadEnabled": false,
          "googlePlayPackageName": "com.company.game",
          "googlePlayServiceAccountJsonPath": "~/Secrets/google-play-service-account.json",
          "googlePlayTrack": "internal",
          "googlePlayReleaseStatus": "draft",
          "googlePlayReleaseName": "",
          "googlePlayUploadArtifact": "aab",
          "googlePlayChangesNotSentForReview": false,
          "googlePlayUserFraction": null,

          "allowProvisioningUpdates": true,
          "resetRepository": false,
          "preserveUnityLibraryOnReset": true,
          "saveConfigSnapshot": true,

          "environment": {}
        }
        """;
}
