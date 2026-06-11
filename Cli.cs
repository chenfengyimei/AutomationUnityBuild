using System.Text.Json;

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

        return await ExecuteAsync(args);
    }

    internal static async Task<int> ExecuteAsync(string[] args)
    {
        string command = args[0].Trim().ToLowerInvariant();
        CliOptions options = CliOptions.Parse(args.Skip(1));

        try
        {
            return command switch
            {
                "run" => await RunWorkflowAsync(options),
                "doctor" => await RunDoctorAsync(options),
                "init-config" => InitConfig(options),
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
        BuildConfig config = BuildConfig.Load(options.ConfigPath);
        using var workflow = new AutomationWorkflow(config, options);
        await workflow.RunAsync();
        return 0;
    }

    private static async Task<int> RunDoctorAsync(CliOptions options)
    {
        BuildConfig config = BuildConfig.Load(options.ConfigPath);
        using var workflow = new AutomationWorkflow(config, options with { DryRun = true });
        await workflow.CheckPrerequisitesAsync();
        return 0;
    }

    private static int InitConfig(CliOptions options)
    {
        string path = Path.GetFullPath(options.ConfigPath);
        if (File.Exists(path) && !options.Force)
        {
            throw new InvalidOperationException($"{path} 已存在。需要覆盖时加 --force。");
        }

        File.WriteAllText(path, SampleFiles.BuildIosConfigJson);
        Console.WriteLine($"已生成配置模板: {path}");
        return 0;
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
            Console.WriteLine("  1. 生成 build-ios.json 配置模板");
            Console.WriteLine("  2. 在 Windows 上预览打包命令 dry-run");
            Console.WriteLine("  3. 手动输入完整命令");
            Console.WriteLine("  0. 退出");
            Console.Write("> ");

            string? choice = Console.ReadLine();
            switch (choice?.Trim())
            {
                case "1":
                    await Cli.ExecuteAsync(["init-config", "--config", "build-ios.json"]);
                    break;
                case "2":
                    string configPath = File.Exists("build-ios.json") ? "build-ios.json" : "build-ios.sample.json";
                    await Cli.ExecuteAsync(["run", "--config", configPath, "--dry-run", "--allow-non-mac", "--verbose"]);
                    break;
                case "3":
                    Console.WriteLine("请输入命令，不需要输入 exe 名称。例: run --config build-ios.json --dry-run --allow-non-mac");
                    Console.Write("> ");
                    string? commandLine = Console.ReadLine();
                    string[] args = CommandLineParser.Split(commandLine);
                    if (args.Length > 0)
                    {
                        await Cli.ExecuteAsync(args);
                    }
                    break;
                case "0":
                case "":
                case null:
                    return 0;
                default:
                    Console.WriteLine("请输入 1、2、3 或 0。");
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
    bool DryRun,
    bool Force,
    bool SkipGit,
    bool SkipUnity,
    bool SkipXcode,
    bool AllowNonMac,
    bool Verbose)
{
    public static CliOptions Parse(IEnumerable<string> args)
    {
        string configPath = "build-ios.json";
        bool dryRun = false;
        bool force = false;
        bool skipGit = false;
        bool skipUnity = false;
        bool skipXcode = false;
        bool allowNonMac = false;
        bool verbose = false;

        using IEnumerator<string> enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            string arg = enumerator.Current;
            switch (arg)
            {
                case "--config":
                case "-c":
                    configPath = NextValue(enumerator, arg);
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
                default:
                    throw new ArgumentException($"无法识别参数: {arg}");
            }
        }

        return new CliOptions(
            configPath,
            dryRun,
            force,
            skipGit,
            skipUnity,
            skipXcode,
            allowNonMac,
            verbose);
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
              AutomationUnityBuildIOS run --config build-ios.json
              AutomationUnityBuildIOS doctor --config build-ios.json
              AutomationUnityBuildIOS init-config --config build-ios.json

            常用参数:
              --dry-run        只打印将要执行的命令，不真正执行
              --skip-git       跳过 git 拉取/更新
              --skip-unity     跳过 Unity 导出 Xcode 工程
              --skip-xcode     跳过 Xcode archive/export
              --allow-non-mac  允许在非 macOS 上 dry-run 或调试配置
              --verbose        输出更详细的路径与命令
            """);
    }
}
