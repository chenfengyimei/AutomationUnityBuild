using System.Text;

namespace BuildServer.Services;

public static class LogFileReader
{
    public static string ReadAll(string path)
    {
        using FileStream stream = OpenSharedRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static string Tail(string path, int lines)
    {
        using FileStream stream = OpenSharedRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        Queue<string> queue = new();

        while (reader.ReadLine() is { } line)
        {
            queue.Enqueue(line);
            while (queue.Count > lines)
            {
                queue.Dequeue();
            }
        }

        return string.Join(Environment.NewLine, queue);
    }

    private static FileStream OpenSharedRead(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }
}
