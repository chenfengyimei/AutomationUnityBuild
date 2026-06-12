namespace AutomationUnityBuildIOS;

internal static class ConfigFileSelector
{
    public static IReadOnlyList<string> FindConfigFiles()
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFiles(Environment.CurrentDirectory, "build-ios*.json", files);
        AddFiles(Environment.CurrentDirectory, "*.iosbuild.json", files);

        string configsDirectory = Path.Combine(Environment.CurrentDirectory, "configs");
        AddFiles(configsDirectory, "*.json", files);

        return files
            .Where(file => !Path.GetFileName(file).Equals("build-ios.sample.json", StringComparison.OrdinalIgnoreCase))
            .Select(ToDisplayPath)
            .ToArray();
    }

    public static string SelectConfigFile(string actionName)
    {
        while (true)
        {
            IReadOnlyList<string> configs = FindConfigFiles();
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
                Console.WriteLine($"  {i + 1}. {configs[i]}");
            }

            Console.WriteLine("  0. 初始化新配置");
            Console.WriteLine("也可以直接输入配置文件路径。");
            Console.Write("> ");

            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return configs[0];
            }

            if (input == "0")
            {
                string created = ConfigWizard.Run("build-ios.json", configWasSpecified: false, force: false);
                return created;
            }

            if (int.TryParse(input, out int number) && number >= 1 && number <= configs.Count)
            {
                return configs[number - 1];
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

    private static string ToDisplayPath(string fullPath)
    {
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
    }
}

