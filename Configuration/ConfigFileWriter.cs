using System.Text;
using System.Text.Json;

namespace AutomationUnityBuildIOS;

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions IndentedCamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal static class ConfigFileWriter
{
    public static void Save(string fullPath, BuildConfig config)
    {
        PathTools.EnsureParentDirectory(fullPath);
        string json = JsonSerializer.Serialize(config, JsonOptions.IndentedCamelCase);
        string tempPath = $"{fullPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, json + Environment.NewLine, TextEncodings.Utf8Bom);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}

internal static class TextEncodings
{
    public static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
}

