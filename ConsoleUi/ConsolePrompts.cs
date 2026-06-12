namespace AutomationUnityBuildIOS;

internal static class ConsolePrompts
{
    public static string AskRequired(string label, string hint)
    {
        while (true)
        {
            Console.WriteLine($"{label} ({hint})");
            Console.Write("> ");
            string value = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("这个值必填。");
        }
    }

    public static string AskOptional(string label, string defaultValue)
    {
        Console.Write(string.IsNullOrEmpty(defaultValue) ? $"{label}: " : $"{label} [{defaultValue}]: ");
        string value = Console.ReadLine()?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public static string AskChoice(string label, IReadOnlyList<string> choices, string defaultValue)
    {
        while (true)
        {
            Console.WriteLine($"{label} [{defaultValue}]");
            for (int i = 0; i < choices.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {choices[i]}");
            }

            Console.Write("> ");
            string value = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (int.TryParse(value, out int number) && number >= 1 && number <= choices.Count)
            {
                return choices[number - 1];
            }

            string? match = choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            Console.WriteLine("请输入列表编号或列表中的值。");
        }
    }

    public static bool AskBool(string label, bool defaultValue)
    {
        string suffix = defaultValue ? "[Y/n]" : "[y/N]";
        while (true)
        {
            Console.Write($"{label} {suffix}: ");
            string value = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (value is "y" or "yes" or "1" or "true" or "是")
            {
                return true;
            }

            if (value is "n" or "no" or "0" or "false" or "否")
            {
                return false;
            }

            Console.WriteLine("请输入 y 或 n。");
        }
    }
}

