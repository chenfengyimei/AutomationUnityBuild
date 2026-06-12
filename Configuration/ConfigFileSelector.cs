using System.Text.Json;

namespace AutomationUnityBuildIOS;

internal static class ConfigFileSelector
{
    public static IReadOnlyList<ConfigFileEntry> FindConfigFiles()
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFiles(Environment.CurrentDirectory, "build-ios*.json", files);
        AddFiles(Environment.CurrentDirectory, "*.iosbuild.json", files);

        string configsDirectory = Path.Combine(Environment.CurrentDirectory, "configs");
        AddFiles(configsDirectory, "*.json", files);

        return files
            .Where(file => !Path.GetFileName(file).Equals("build-ios.sample.json", StringComparison.OrdinalIgnoreCase))
            .Select(CreateEntry)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string SelectConfigFile(string actionName)
    {
        while (true)
        {
            IReadOnlyList<ConfigFileEntry> configs = FindConfigFiles();
            if (configs.Count == 0)
            {
                Console.WriteLine("没有找到可用配置文件。");
                if (ConsolePrompts.AskBool("是否现在初始化一个新配置", true))
                {
                    string created = ConfigWizard.Run("build-ios.json", configWasSpecified: false, force: false);
                    return created;
                }

                throw new FileNotFoundException("没有选择配置文件。");
            }

            Console.WriteLine();
            Console.WriteLine($"请选择用于{actionName}的配置文件:");
            for (int i = 0; i < configs.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {configs[i].DisplayText}");
            }

            Console.WriteLine("  0. 初始化新配置");
            Console.WriteLine("也可以直接输入配置文件路径。");
            Console.Write("> ");

            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return configs[0].FullPath;
            }

            if (input == "0")
            {
                string created = ConfigWizard.Run("build-ios.json", configWasSpecified: false, force: false);
                return created;
            }

            if (int.TryParse(input, out int number) && number >= 1 && number <= configs.Count)
            {
                return configs[number - 1].FullPath;
            }

            string candidate = Path.GetFullPath(input);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            Console.WriteLine($"找不到配置文件: {input}");
        }
    }

    private static void AddFiles(string directory, string pattern, SortedSet<string> files)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            files.Add(Path.GetFullPath(file));
        }
    }

    private static ConfigFileEntry CreateEntry(string fullPath)
    {
        string displayPath = ToDisplayPath(fullPath);
        string displayName = ReadDisplayName(fullPath);
        return new ConfigFileEntry(fullPath, displayPath, displayName);
    }

    private static string ToDisplayPath(string fullPath)
    {
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
    }

    private static string ReadDisplayName(string fullPath)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
            JsonElement root = document.RootElement;
            return FirstNonEmpty(
                ReadString(root, "configName"),
                ReadString(root, "productName"),
                ReadString(root, "projectDirectoryName"),
                Path.GetFileNameWithoutExtension(fullPath));
        }
        catch
        {
            return $"{Path.GetFileNameWithoutExtension(fullPath)} (配置读取失败)";
        }
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return value.GetString()?.Trim() ?? "";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "未命名配置";
    }
}

internal sealed record ConfigFileEntry(string FullPath, string DisplayPath, string DisplayName)
{
    public string DisplayText => $"{DisplayName} ({DisplayPath})";
}
