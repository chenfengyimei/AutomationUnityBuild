namespace AutomationUnityBuildIOS;

internal static class SampleFiles
{
    public const string BuildIosConfigJson =
        """
        {
          "repositoryUrl": "git@github.com:your-org/your-unity-game.git",
          "branch": "main",
          "workspaceRoot": "~/UnityBuildWorkspace",
          "projectDirectoryName": "YourUnityGame",
          "unityProjectRelativePath": ".",

          "unityVersion": "6000.0.0f1",
          "unityExecutablePath": "",
          "unityBuildMethod": "BuildAutomation.IOSBuilder.Build",

          "artifactsRoot": "~/UnityBuildArtifacts/YourUnityGame",
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
          "compileBitcode": null,
          "uploadSymbols": true,

          "xcodeBuildSettings": {},
          "environment": {},
          "provisioningProfiles": {}
        }
        """;
}
