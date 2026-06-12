namespace AutomationUnityBuildIOS;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            HelpPrinter.Print();
            if (Console.IsInputRedirected)
            {
                return 0;
            }

            return await InteractiveMenu.RunAsync();
        }

        if (IsHelp(args[0]))
        {
            HelpPrinter.Print();
            return 0;
        }

        if (ShortcutCommands.IsShortcut(args[0]))
        {
            return await ShortcutCommands.ExecuteAsync(args);
        }

        return await ExecuteAsync(args);
    }

    internal static async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            if (ShortcutCommands.IsShortcut(args[0]))
            {
                return await ShortcutCommands.ExecuteAsync(args);
            }

            string command = args[0].Trim().ToLowerInvariant();
            CliOptions options = CliOptions.Parse(args.Skip(1));

            return command switch
            {
                "run" => await RunWorkflowAsync(options),
                "doctor" => await RunDoctorAsync(options),
                "init-config" => InitConfig(options),
                "edit-config" => EditConfig(options),
                "list-configs" => ListConfigs(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"失败: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private static async Task<int> RunWorkflowAsync(CliOptions options)
    {
        options = options with { ConfigPath = ResolveConfigPath(options, "打包") };
        BuildConfig config = BuildConfig.Load(options.ConfigPath);
        using var workflow = new AutomationWorkflow(config, options);
        await workflow.RunAsync();
        return 0;
    }

    private static async Task<int> RunDoctorAsync(CliOptions options)
    {
        options = options with { ConfigPath = ResolveConfigPath(options, "检查环境") };
        BuildConfig config = BuildConfig.Load(options.ConfigPath);
        using var workflow = new AutomationWorkflow(config, options);
        await workflow.CheckPrerequisitesAsync();
        return 0;
    }

    private static int InitConfig(CliOptions options)
    {
        if (options.Template || Console.IsInputRedirected)
        {
            return InitTemplateConfig(options);
        }

        ConfigWizard.Run(options.ConfigPath, options.ConfigWasSpecified, options.Force);
        return 0;
    }

    private static int EditConfig(CliOptions options)
    {
        string configPath = ResolveConfigPath(options, "修改");
        ConfigEditor.Run(configPath);
        return 0;
    }

    private static int InitTemplateConfig(CliOptions options)
    {
        string path = Path.GetFullPath(string.IsNullOrWhiteSpace(options.ConfigPath) ? "build-ios.json" : options.ConfigPath);
        if (File.Exists(path) && !options.Force)
        {
            throw new InvalidOperationException($"{path} 已存在。需要覆盖时加 --force。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SampleFiles.BuildIosConfigJson, TextEncodings.Utf8Bom);
        Console.WriteLine($"已生成配置模板: {path}");
        return 0;
    }

    private static int ListConfigs()
    {
        IReadOnlyList<ConfigFileEntry> configs = ConfigFileSelector.FindConfigFiles();
        if (configs.Count == 0)
        {
            Console.WriteLine("没有找到配置文件。可以先运行 init-config 创建。");
            return 0;
        }

        Console.WriteLine("可用配置文件:");
        for (int i = 0; i < configs.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {configs[i].DisplayText}");
        }

        return 0;
    }

    private static string ResolveConfigPath(CliOptions options, string actionName)
    {
        if (options.ConfigWasSpecified)
        {
            return options.ConfigPath;
        }

        if (Console.IsInputRedirected)
        {
            return File.Exists("build-ios.json") ? "build-ios.json" : options.ConfigPath;
        }

        return ConfigFileSelector.SelectConfigFile(actionName);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"未知命令: {command}");
        HelpPrinter.Print();
        return 1;
    }
}

