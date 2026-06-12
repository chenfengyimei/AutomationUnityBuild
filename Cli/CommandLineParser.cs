namespace AutomationUnityBuildIOS;

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

