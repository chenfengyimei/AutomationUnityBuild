using System.Collections.ObjectModel;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using AutomationUnityBuildIOS;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class EmailSettingsPageViewModel : ViewModelBase
{
    private static readonly int[] ImplicitSslPorts = [465, 993, 995];

    private string _settingsPath;
    private string _smtpHost = "";
    private int _smtpPort = 587;
    private string _smtpUserName = "";
    private string _smtpPassword = "";
    private string _fromEmail = "";
    private string _fromName = "";
    private bool _useSsl = true;
    private bool _enabled;
    private string _testEmailTo = "";
    private string _statusMessage = "配置 SMTP 发信账号，保存后可发送测试邮件。";
    private bool _isBusy;

    public string SmtpHost { get => _smtpHost; set => Set(ref _smtpHost, value); }
    public int SmtpPort { get => _smtpPort; set => Set(ref _smtpPort, value); }
    public string SmtpUserName { get => _smtpUserName; set => Set(ref _smtpUserName, value); }
    public string SmtpPassword { get => _smtpPassword; set => Set(ref _smtpPassword, value); }
    public string FromEmail { get => _fromEmail; set => Set(ref _fromEmail, value); }
    public string FromName { get => _fromName; set => Set(ref _fromName, value); }
    public bool UseSsl { get => _useSsl; set => Set(ref _useSsl, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string TestEmailTo { get => _testEmailTo; set => Set(ref _testEmailTo, value); }
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

    public ObservableCollection<NotificationContact> Contacts { get; } = new();

    public EmailSettingsPageViewModel()
    {
        _settingsPath = DesktopPaths.EmailSettingsPath;
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                SmtpHost = GetString(root, "smtpHost");
                SmtpPort = GetInt(root, "smtpPort", 587);
                SmtpUserName = GetString(root, "smtpUserName");
                SmtpPassword = GetString(root, "smtpPassword");
                FromEmail = GetString(root, "fromEmail");
                FromName = GetString(root, "fromName");
                UseSsl = GetBool(root, "useSsl", true);
                Enabled = GetBool(root, "enabled", false);

                Contacts.Clear();
                if (root.TryGetProperty("contacts", out var contactsEl) && contactsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in contactsEl.EnumerateArray())
                    {
                        Contacts.Add(new NotificationContact
                        {
                            Title = GetString(c, "title"),
                            Email = GetString(c, "email"),
                            Enabled = GetBool(c, "enabled", true)
                        });
                    }
                }
            }
        }
        catch { }
    }

    public void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var data = new
            {
                smtpHost = SmtpHost,
                smtpPort = SmtpPort,
                smtpUserName = SmtpUserName,
                smtpPassword = SmtpPassword,
                fromEmail = FromEmail,
                fromName = FromName,
                useSsl = UseSsl,
                enabled = Enabled,
                contacts = Contacts.Select(c => new { title = c.Title, email = c.Email, enabled = c.Enabled }).ToList()
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            StatusMessage = "✅ 邮件设置已保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 保存失败: {ex.Message}";
        }
    }

    public async Task SendTestEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(TestEmailTo))
        {
            StatusMessage = "请填写收件邮箱。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在发送测试邮件...";

        try
        {
            await SendEmailAsync(TestEmailTo, "[DesktopApp] 测试邮件",
                "这是一封来自 DesktopApp 的测试邮件，确认邮件通知配置正确。");
            StatusMessage = "✅ 测试邮件已发送，请检查收件箱。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 发送失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AddContact(string title, string email)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(email)) return;
        Contacts.Add(new NotificationContact { Title = title, Email = email, Enabled = true });
        StatusMessage = $"已添加联系人: {title} <{email}>";
    }

    public void RemoveContact(NotificationContact contact)
    {
        Contacts.Remove(contact);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        bool useImplicitSsl = UseSsl && ImplicitSslPorts.Contains(SmtpPort);

        if (useImplicitSsl)
        {
            await SendWithImplicitSslAsync(toEmail, subject, body);
        }
        else
        {
            using var message = new MailMessage();
            message.From = string.IsNullOrWhiteSpace(FromName)
                ? new MailAddress(FromEmail)
                : new MailAddress(FromEmail, FromName, Encoding.UTF8);
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = body;
            message.BodyEncoding = Encoding.UTF8;
            message.SubjectEncoding = Encoding.UTF8;

            using var client = new SmtpClient(SmtpHost, SmtpPort);
            client.EnableSsl = UseSsl;
            if (!string.IsNullOrWhiteSpace(SmtpUserName))
                client.Credentials = new NetworkCredential(SmtpUserName, SmtpPassword);

            await client.SendMailAsync(message);
        }
    }

    private async Task SendWithImplicitSslAsync(string toEmail, string subject, string body)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(SmtpHost, SmtpPort);
        using var sslStream = new SslStream(tcpClient.GetStream(), false, (_, _, _, _) => true);
        await sslStream.AuthenticateAsClientAsync(SmtpHost);
        using var reader = new StreamReader(sslStream, Encoding.ASCII);
        using var writer = new StreamWriter(sslStream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

        await ExpectResponseAsync(reader, 220);
        await SendCmdAsync(writer, reader, $"EHLO {SmtpHost}");

        if (!string.IsNullOrWhiteSpace(SmtpUserName))
        {
            await SendCmdAsync(writer, reader, "AUTH LOGIN");
            await SendCmdAsync(writer, reader, Convert.ToBase64String(Encoding.UTF8.GetBytes(SmtpUserName)));
            await SendCmdAsync(writer, reader, Convert.ToBase64String(Encoding.UTF8.GetBytes(SmtpPassword)));
        }

        await SendCmdAsync(writer, reader, $"MAIL FROM:<{FromEmail}>");
        await SendCmdAsync(writer, reader, $"RCPT TO:<{toEmail}>");
        await SendCmdAsync(writer, reader, "DATA");

        await writer.WriteLineAsync($"From: {FormatAddr(FromEmail, FromName)}");
        await writer.WriteLineAsync($"To: <{toEmail}>");
        await writer.WriteLineAsync($"Subject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=");
        await writer.WriteLineAsync("MIME-Version: 1.0");
        await writer.WriteLineAsync("Content-Type: text/plain; charset=utf-8");
        await writer.WriteLineAsync("Content-Transfer-Encoding: base64");
        await writer.WriteLineAsync();

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var base64 = Convert.ToBase64String(bodyBytes);
        for (int i = 0; i < base64.Length; i += 76)
            await writer.WriteLineAsync(base64.Substring(i, Math.Min(76, base64.Length - i)));

        await SendCmdAsync(writer, reader, "\r\n.");
        await writer.WriteLineAsync("QUIT");
    }

    private static async Task SendCmdAsync(StreamWriter w, StreamReader r, string cmd)
    {
        await w.WriteLineAsync(cmd);
        if (cmd != "DATA" && cmd != "QUIT")
            await ExpectResponseAsync(r);
    }

    private static async Task ExpectResponseAsync(StreamReader r, int? code = null)
    {
        string line;
        do { line = await r.ReadLineAsync() ?? ""; }
        while (line.Length >= 4 && line[3] == '-');
        if (code is not null && !line.StartsWith(code.Value.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException($"SMTP 异常: {line}");
    }

    private static string FormatAddr(string email, string? name)
        => string.IsNullOrWhiteSpace(name) ? $"<{email}>" : $"\"{name}\" <{email}>";

    private static string GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static int GetInt(JsonElement el, string name, int def)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static bool GetBool(JsonElement el, string name, bool def)
        => el.TryGetProperty(name, out var v) && (v.ValueKind is JsonValueKind.True or JsonValueKind.False) ? v.GetBoolean() : def;
}

public class NotificationContact
{
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
