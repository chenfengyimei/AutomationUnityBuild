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
          "resetRepository": true,
          "preserveUnityLibraryOnReset": true,
          "cleanXcodeOutputBeforeBuild": true,
          "useWorkspaceIfPresent": true,
          "generateExportOptionsPlist": true,
          "copyArchiveToOrganizer": true,
          "saveConfigSnapshot": true,
          "compileBitcode": null,
          "uploadSymbols": true,
          "appStoreConnectUploadEnabled": false,
          "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
          "appStoreConnectApiKeyId": "",
          "appStoreConnectApiIssuerId": "",

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
          "resetRepository": true,
          "preserveUnityLibraryOnReset": true,
          "saveConfigSnapshot": true,

          "environment": {}
        }
        """;

    public const string BuildTiktokConfigJson =
        """
        {
          "configName": "tiktok-release",
          "buildPlatform": "tiktok",
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
          "unityBuildMethod": "BuildAutomation.TiktokBuilder.Build",

          "artifactsRoot": "~/UnityBuildArtifacts/YourUnityGame-Tiktok",
          "allowedArtifactsRoots": [
            "~/UnityBuildArtifacts/YourUnityGame-Tiktok"
          ],
          "logsDirectory": "",

          "bundleIdentifier": "com.company.game",
          "productName": "Your Game",
          "bundleVersion": "1.0.0",
          "syncBundleVersionFromUnity": true,
          "buildNumber": "1",
          "autoIncrementBuildNumber": true,

          "tiktokAppId": "",
          "tiktokAccessToken": "",
          "tiktokGameName": "",
          "tiktokWebglOutputDirectory": "",
          "tiktokUploadEnabled": false,
          "tiktokApiEndpoint": "https://open-api.tiktokglobalshop.com",

          "resetRepository": true,
          "preserveUnityLibraryOnReset": true,
          "saveConfigSnapshot": true,

          "environment": {}
        }
        """;
}
