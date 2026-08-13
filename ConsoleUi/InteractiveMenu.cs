namespace AutomationUnityBuildIOS;

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
            Console.WriteLine("  10. 选择配置并修改配置内容");
            Console.WriteLine("  11. 生成 Android 配置模板");
            Console.WriteLine("  12. 生成 TikTok 配置模板");
            Console.WriteLine("  99. 手动输入完整命令");
            Console.WriteLine("  0. 退出");
            Console.Write("> ");

            string choice = Console.ReadLine()?.Trim() ?? "";
            if (choice == "0" || choice.Length == 0)
            {
                return 0;
            }

            if (ShortcutCommands.IsShortcut(choice))
            {
                await ShortcutCommands.ExecuteAsync([choice]);
                continue;
            }

            switch (choice)
            {
                case "99":
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

