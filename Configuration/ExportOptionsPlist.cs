using System.Security;
using System.Text;

namespace AutomationUnityBuildIOS;

internal static class ExportOptionsPlist
{
    public static void Write(BuildConfig config, string path, string? destination = null)
    {
        PathTools.EnsureParentDirectory(path);

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        builder.AppendLine("<plist version=\"1.0\">");
        builder.AppendLine("<dict>");

        AppendString(builder, "method", config.ExportMethod);
        AppendString(builder, "destination", destination ?? "");
        AppendString(builder, "teamID", config.TeamId);
        AppendString(builder, "signingStyle", config.SigningStyle);
        AppendNullableBool(builder, "compileBitcode", config.CompileBitcode);
        AppendNullableBool(builder, "uploadSymbols", config.UploadSymbols);

        if (config.ProvisioningProfiles.Count > 0)
        {
            AppendKey(builder, "provisioningProfiles");
            builder.AppendLine("<dict>");
            foreach ((string bundleId, string profileName) in config.ProvisioningProfiles.OrderBy(pair => pair.Key))
            {
                AppendString(builder, bundleId, profileName);
            }
            builder.AppendLine("</dict>");
        }

        builder.AppendLine("</dict>");
        builder.AppendLine("</plist>");

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void AppendString(StringBuilder builder, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AppendKey(builder, key);
        builder.AppendLine($"<string>{Escape(value)}</string>");
    }

    private static void AppendNullableBool(StringBuilder builder, string key, bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        AppendKey(builder, key);
        builder.AppendLine(value.Value ? "<true/>" : "<false/>");
    }

    private static void AppendKey(StringBuilder builder, string key)
    {
        builder.AppendLine($"<key>{Escape(key)}</key>");
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? "";
    }
}
