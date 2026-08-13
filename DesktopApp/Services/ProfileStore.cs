using System.Text.Json;
using DesktopApp.Models;

namespace DesktopApp.Services;

/// <summary>
/// 持久化管理项目模板和证书模板，数据存储在 profiles/ 目录下。
/// </summary>
public static class ProfileStore
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string ProfilesDir => Path.Combine(Environment.CurrentDirectory, "profiles");

    private static string ProjectsPath => Path.Combine(ProfilesDir, "projects.json");
    private static string CertificatesPath => Path.Combine(ProfilesDir, "certificates.json");

    // ---- Project Profiles ----

    public static List<ProjectProfile> LoadProjects()
    {
        return Load<ProjectProfile>(ProjectsPath);
    }

    public static void SaveProjects(List<ProjectProfile> profiles)
    {
        Save(ProjectsPath, profiles);
    }

    // ---- Certificate Profiles ----

    public static List<CertificateProfile> LoadCertificates()
    {
        return Load<CertificateProfile>(CertificatesPath);
    }

    public static void SaveCertificates(List<CertificateProfile> profiles)
    {
        Save(CertificatesPath, profiles);
    }

    // ---- Generic helpers ----

    private static List<T> Load<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new List<T>();

            string json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<T>>(json, s_jsonOpts);
            return list ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private static void Save<T>(string path, List<T> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(items, s_jsonOpts);
        File.WriteAllText(path, json + Environment.NewLine);
    }
}
