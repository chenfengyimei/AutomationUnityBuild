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
        using var workflow = new AutomationWorkflow(config, options with { DryRun = true });
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
        IReadOnlyList<string> configs = ConfigFileSelector.FindConfigFiles();
        if (configs.Count == 0)
        {
            Console.WriteLine("没有找到配置文件。可以先运行 init-config 创建。");
            return 0;
        }

        Console.WriteLine("可用配置文件:");
        for (int i = 0; i < configs.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {configs[i]}");
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

internal static class InteractiveMenu
{
    public static async Task<int> RunAsync()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("请选择:");
            Console.WriteLine("  00. 帮助和快捷指令");
            Console.WriteLine("  01. 初始化新配置");
            Console.WriteLine("  02. 生成空配置模板");
            Console.WriteLine("  03. 查看已有配置");
            Console.WriteLine("  04. 选择配置并检查环境");
            Console.WriteLine("  05. 选择配置并预览命令 dry-run");
            Console.WriteLine("  06. 选择配置并正式打包");
            Console.WriteLine("  10. 手动输入完整命令");
            Console.WriteLine("  0. 退出");
            Console.Write("> ");

            string choice = Console.ReadLine()?.Trim() ?? "";
            if (choice == "0" || choice.Length == 0)
            {
                return 0;
            }

            if (ShortcutCommands.IsShortcut(choice))
            {
                await Cli.ExecuteAsync([choice]);
                continue;
            }

            switch (choice)
            {
                case "1":
                    await Cli.ExecuteAsync(["init-config"]);
                    break;
                case "2":
                    await Cli.ExecuteAsync(["init-config", "--config", "build-ios.json", "--template"]);
                    break;
                case "3":
                    await Cli.ExecuteAsync(["list-configs"]);
                    break;
                case "4":
                    await Cli.ExecuteAsync(["doctor", "--allow-non-mac"]);
                    break;
                case "5":
                    await Cli.ExecuteAsync(["run", "--dry-run", "--verbose", "--allow-non-mac"]);
                    break;
                case "6":
                    await Cli.ExecuteAsync(["run"]);
                    break;
                case "10":
                    Console.WriteLine("请输入命令，不需要输入 exe 名称。例: run --config configs/build-ios.dev.json --dry-run --allow-non-mac");
                    Console.Write("> ");
                    string? commandLine = Console.ReadLine();
                    string[] args = CommandLineParser.Split(commandLine);
                    if (args.Length > 0)
                    {
                        await Cli.ExecuteAsync(args);
                    }
                    break;
                default:
                    Console.WriteLine("请输入快捷编号，例如 00、01、05，或输入 0 退出。");
                    break;
            }
        }
    }

}

internal static class CommandLineParser
{
    public static string[] Split(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                AddCurrent();
                continue;
            }

            current.Append(c);
        }

        AddCurrent();
        return args.ToArray();

        void AddCurrent()
        {
            if (current.Length == 0)
            {
                return;
            }

            args.Add(current.ToString());
            current.Clear();
        }
    }
}

internal sealed record CliOptions(
    string ConfigPath,
    bool ConfigWasSpecified,
    bool DryRun,
    bool Force,
    bool SkipGit,
    bool SkipUnity,
    bool SkipXcode,
    bool AllowNonMac,
    bool Verbose,
    bool Template)
{
    public static CliOptions Parse(IEnumerable<string> args)
    {
        string configPath = "build-ios.json";
        bool configWasSpecified = false;
        bool dryRun = false;
        bool force = false;
        bool skipGit = false;
        bool skipUnity = false;
        bool skipXcode = false;
        bool allowNonMac = false;
        bool verbose = false;
        bool template = false;

        using IEnumerator<string> enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            string arg = enumerator.Current;
            switch (arg)
            {
                case "--config":
                case "-c":
                    configPath = NextValue(enumerator, arg);
                    configWasSpecified = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--skip-git":
                    skipGit = true;
                    break;
                case "--skip-unity":
                    skipUnity = true;
                    break;
                case "--skip-xcode":
                    skipXcode = true;
                    break;
                case "--allow-non-mac":
                    allowNonMac = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--template":
                    template = true;
                    break;
                default:
                    throw new ArgumentException($"无法识别参数: {arg}");
            }
        }

        return new CliOptions(
            configPath,
            configWasSpecified,
            dryRun,
            force,
            skipGit,
            skipUnity,
            skipXcode,
            allowNonMac,
            verbose,
            template);
    }

    private static string NextValue(IEnumerator<string> enumerator, string optionName)
    {
        if (!enumerator.MoveNext() || string.IsNullOrWhiteSpace(enumerator.Current))
        {
            throw new ArgumentException($"{optionName} 后面需要一个值。");
        }

        return enumerator.Current;
    }
}

internal static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine(
            """
            Unity iOS 自动化打包工具

            用法:
              AutomationUnityBuildIOS init-config
              AutomationUnityBuildIOS list-configs
              AutomationUnityBuildIOS run
              AutomationUnityBuildIOS run --config configs/build-ios.dev.json
              AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json

            初始化:
              init-config       进入问答式配置向导，生成已填写好的配置文件
              --template        只生成模板文件，不进入问答
              --force           允许覆盖已有配置文件

            常用参数:
              --config, -c      指定配置文件；不指定时会进入选择列表
              --dry-run         只打印将要执行的命令，不真正执行
              --skip-git        跳过 git 拉取/更新
              --skip-unity      跳过 Unity 导出 Xcode 工程
              --skip-xcode      跳过 Xcode archive/export
              --allow-non-mac   允许在非 macOS 上 dry-run 或调试配置
              --verbose         输出更详细的路径与命令
            """);
        ShortcutCommands.PrintTable();
    }
}
