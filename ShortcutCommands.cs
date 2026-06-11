namespace AutomationUnityBuildIOS;

internal sealed record ShortcutCommand(string Code, string Description, string[] Args);

internal static class ShortcutCommands
{
    private static readonly ShortcutCommand[] Commands =
    [
        new("01", "初始化配置向导，问答生成可直接使用的配置文件", ["init-config"]),
        new("02", "生成空配置模板 build-ios.json", ["init-config", "--config", "build-ios.json", "--template"]),
        new("03", "查看已有配置文件", ["list-configs"]),
        new("04", "选择配置并检查环境", ["doctor", "--allow-non-mac"]),
        new("05", "选择配置并预览完整打包命令 dry-run", ["run", "--dry-run", "--verbose", "--allow-non-mac"]),
        new("06", "选择配置并执行完整打包流程", ["run"]),
        new("07", "选择配置打包，但跳过 Git 同步", ["run", "--skip-git"]),
        new("08", "选择配置打包，但跳过 Unity 导出", ["run", "--skip-unity"]),
        new("09", "选择配置打包，但跳过 Xcode 编译导出", ["run", "--skip-xcode"])
    ];

    public static bool IsShortcut(string? value)
    {
        return TryNormalize(value, out string code) && (code == "00" || Commands.Any(command => command.Code == code));
    }

    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (!TryNormalize(args.FirstOrDefault(), out string code))
        {
            return 1;
        }

        if (code == "00")
        {
            HelpPrinter.Print();
            return 0;
        }

        ShortcutCommand command = Commands.First(command => command.Code == code);
        string[] extraArgs = args.Skip(1).ToArray();
        string[] expandedArgs = command.Args.Concat(extraArgs).ToArray();

        Console.WriteLine($"快捷指令 {command.Code}: {command.Description}");
        Console.WriteLine($"等价命令: {CommandLineFormatter.Format("AutomationUnityBuildIOS", expandedArgs)}");
        Console.WriteLine();

        return await Cli.ExecuteAsync(expandedArgs);
    }

    public static void PrintTable()
    {
        Console.WriteLine();
        Console.WriteLine("快捷指令:");
        Console.WriteLine("  00  显示帮助和快捷指令表");
        foreach (ShortcutCommand command in Commands)
        {
            Console.WriteLine($"  {command.Code}  {command.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("快捷指令也可以追加参数，例如:");
        Console.WriteLine("  05 --config configs/build-ios.dev.json");
        Console.WriteLine("  06 --config configs/build-ios.release.json");
    }

    private static bool TryNormalize(string? value, out string code)
    {
        code = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (!trimmed.All(char.IsDigit))
        {
            return false;
        }

        code = trimmed.Length == 1 ? "0" + trimmed : trimmed;
        return code.Length == 2;
    }
}
