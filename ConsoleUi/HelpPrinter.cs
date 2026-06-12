namespace AutomationUnityBuildIOS;

internal static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine(
            """
            Unity iOS 自动化打包工具

            用法:
              AutomationUnityBuildIOS init-config
              AutomationUnityBuildIOS edit-config
              AutomationUnityBuildIOS list-configs
              AutomationUnityBuildIOS run
              AutomationUnityBuildIOS run --config configs/build-ios.dev.json
              AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json

            初始化:
              init-config       进入问答式配置向导，生成已填写好的配置文件
              edit-config       选择并修改已有配置文件中的常用字段
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

