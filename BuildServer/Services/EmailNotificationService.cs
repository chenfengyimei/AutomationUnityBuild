using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using BuildServer.Persistence;

namespace BuildServer.Services;

public sealed class EmailNotificationService(
    JsonDatabase database,
    ILogger<EmailNotificationService> logger)
{
    private static readonly int[] ImplicitSslPorts = [465, 993, 995];

    public async Task SendBuildNotificationAsync(BuildJobRecord job, string projectName, string configName)
    {
        if (job.NotifyEmails is null || job.NotifyEmails.Count == 0)
        {
            return;
        }

        EmailSettingsRecord? settings = await database.ReadAsync(db => db.EmailSettings);
        if (settings is null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            logger.LogInformation("邮件通知已跳过：邮件设置未启用或 SMTP 主机为空。");
            return;
        }

        List<NotificationContactRecord> contacts = await database.ReadAsync(db => db.NotificationContacts);

        bool succeeded = job.Status == BuildStatuses.Succeeded;
        string statusText = succeeded ? "打包成功" : "打包失败";
        string subject = $"[{statusText}] {projectName}/{configName} #{job.BuildNumber}";

        logger.LogInformation("准备发送打包通知邮件，收件人 {Count} 个: {Recipients}",
            job.NotifyEmails.Count, string.Join(", ", job.NotifyEmails));

        foreach (string recipient in job.NotifyEmails.Where(email => !string.IsNullOrWhiteSpace(email)))
        {
            NotificationContactRecord? contact = contacts.FirstOrDefault(c =>
                string.Equals(c.Email, recipient, StringComparison.OrdinalIgnoreCase));

            string body = BuildNotificationBody(job, projectName, configName, contact?.Title);
            try
            {
                await SendEmailAsync(settings, recipient, subject, body);
                logger.LogInformation("打包通知邮件已发送至 {Recipient}", recipient);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "发送打包通知邮件至 {Recipient} 失败", recipient);
            }
        }
    }

    public async Task<(bool Success, string Error)> SendTestEmailAsync(string toEmail)
    {
        EmailSettingsRecord? settings = await database.ReadAsync(db => db.EmailSettings);
        if (settings is null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            return (false, "邮件设置未启用或 SMTP 主机为空。");
        }

        try
        {
            await SendEmailAsync(settings, toEmail, "[BuildServer] 测试邮件", "这是一封来自 BuildServer 的测试邮件，确认邮件通知配置正确。");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string BuildNotificationBody(BuildJobRecord job, string projectName, string configName, string? contactTitle)
    {
        bool succeeded = job.Status == BuildStatuses.Succeeded;
        StringBuilder sb = new();

        string recipientName = string.IsNullOrWhiteSpace(contactTitle) ? "各位" : contactTitle;
        sb.AppendLine($"尊敬的「{projectName}」{recipientName}：");
        sb.AppendLine();
        if (succeeded)
        {
            sb.AppendLine($"您关注的「{projectName}」全自动化打包流程已完成，本次构建结果为 成功。");
            sb.AppendLine("以下是本次构建的详细信息，请查阅。");
        }
        else
        {
            sb.AppendLine($"您关注的「{projectName}」全自动化打包流程已完成，本次构建结果为 失败。");
            sb.AppendLine("请尽快查看下方错误信息并前往控制台排查处理。");
        }
        sb.AppendLine();
        sb.AppendLine("──────── 构建信息 ────────");
        sb.AppendLine($"项目: {projectName}");
        sb.AppendLine($"配置: {configName}");
        sb.AppendLine($"平台: {job.BuildPlatform}");
        sb.AppendLine($"分支: {job.Branch}");
        sb.AppendLine($"Build Number: {job.BuildNumber}");
        sb.AppendLine($"开始时间: {job.StartedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"完成时间: {job.FinishedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        if (job.DryRun)
        {
            sb.AppendLine("模式: 演练模式 (dry-run)");
        }
        if (!string.IsNullOrWhiteSpace(job.Notes))
        {
            sb.AppendLine($"备注: {job.Notes}");
        }
        if (!succeeded && !string.IsNullOrWhiteSpace(job.Error))
        {
            sb.AppendLine();
            sb.AppendLine("──────── 错误信息 ────────");
            sb.AppendLine(job.Error);
        }
        sb.AppendLine();
        sb.AppendLine("请在 BuildServer 控制台查看详细日志和产物。");
        sb.AppendLine();
        sb.AppendLine("此邮件由 BuildServer 自动化打包平台自动发送，请勿直接回复。");
        return sb.ToString();
    }

    private static async Task SendEmailAsync(EmailSettingsRecord settings, string toEmail, string subject, string body)
    {
        bool useImplicitSsl = settings.UseSsl && ImplicitSslPorts.Contains(settings.SmtpPort);

        if (useImplicitSsl)
        {
            await SendWithImplicitSslAsync(settings, toEmail, subject, body);
        }
        else
        {
            await SendWithSmtpClientAsync(settings, toEmail, subject, body);
        }
    }

    /// <summary>
    /// System.Net.Mail.SmtpClient 只支持 STARTTLS（端口 587），
    /// 不支持隐式 SSL（端口 465）。对 465 等隐式 SSL 端口使用 SslStream 手动握手。
    /// </summary>
    private static async Task SendWithImplicitSslAsync(
        EmailSettingsRecord settings, string toEmail, string subject, string body)
    {
        using TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(settings.SmtpHost, settings.SmtpPort);

        using SslStream sslStream = new(
            tcpClient.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, _, _, _) => true);
        await sslStream.AuthenticateAsClientAsync(settings.SmtpHost);

        using StreamReader reader = new(sslStream, Encoding.ASCII);
        using StreamWriter writer = new(sslStream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

        await ExpectResponseAsync(reader, 220);
        await SendCommandAsync(writer, reader, $"EHLO {settings.SmtpHost}");

        if (!string.IsNullOrWhiteSpace(settings.SmtpUserName))
        {
            string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{settings.SmtpUserName}\0{settings.SmtpPassword}"));
            await SendCommandAsync(writer, reader, "AUTH LOGIN");
            await SendCommandAsync(writer, reader, Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.SmtpUserName)));
            await SendCommandAsync(writer, reader, Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.SmtpPassword)));
            await writer.WriteLineAsync($"AUTH PLAIN {token}");
            string authResponse = await reader.ReadLineAsync() ?? "";
            if (!authResponse.StartsWith("235", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"SMTP 认证失败: {authResponse}");
            }
        }

        string fromAddress = settings.FromEmail;
        await SendCommandAsync(writer, reader, $"MAIL FROM:<{fromAddress}>");
        await SendCommandAsync(writer, reader, $"RCPT TO:<{toEmail}>");
        await SendCommandAsync(writer, reader, "DATA");

        await writer.WriteLineAsync($"From: {FormatAddress(fromAddress, settings.FromName)}");
        await writer.WriteLineAsync($"To: <{toEmail}>");
        await writer.WriteLineAsync($"Subject: {EncodeSubject(subject)}");
        await writer.WriteLineAsync("MIME-Version: 1.0");
        await writer.WriteLineAsync("Content-Type: text/plain; charset=utf-8");
        await writer.WriteLineAsync("Content-Transfer-Encoding: base64");
        await writer.WriteLineAsync();
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        foreach (string line in Base64Split(bodyBytes, 76))
        {
            await writer.WriteLineAsync(line);
        }

        await SendCommandAsync(writer, reader, "\r\n.");
        await SendCommandAsync(writer, reader, "QUIT");
    }

    private static async Task SendWithSmtpClientAsync(
        EmailSettingsRecord settings, string toEmail, string subject, string body)
    {
        using MailMessage message = new();
        message.From = string.IsNullOrWhiteSpace(settings.FromName)
            ? new MailAddress(settings.FromEmail)
            : new MailAddress(settings.FromEmail, settings.FromName, Encoding.UTF8);
        message.To.Add(toEmail);
        message.Subject = subject;
        message.Body = body;
        message.BodyEncoding = Encoding.UTF8;
        message.SubjectEncoding = Encoding.UTF8;

        using SmtpClient client = new(settings.SmtpHost, settings.SmtpPort);
        client.EnableSsl = settings.UseSsl;
        if (!string.IsNullOrWhiteSpace(settings.SmtpUserName))
        {
            client.Credentials = new NetworkCredential(settings.SmtpUserName, settings.SmtpPassword);
        }

        await client.SendMailAsync(message);
    }

    private static async Task SendCommandAsync(StreamWriter writer, StreamReader reader, string command)
    {
        await writer.WriteLineAsync(command);
        if (command != "DATA" && command != "QUIT")
        {
            await ExpectResponseAsync(reader);
        }
    }

    private static async Task ExpectResponseAsync(StreamReader reader, int? expectedCode = null)
    {
        string line;
        do
        {
            line = await reader.ReadLineAsync() ?? "";
        }
        while (line.Length >= 4 && line[3] == '-');

        if (expectedCode is not null && !line.StartsWith(expectedCode.Value.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"SMTP 服务器返回异常: {line}");
        }
    }

    private static string FormatAddress(string email, string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? $"<{email}>" : $"\"{name}\" <{email}>";
    }

    private static string EncodeSubject(string subject)
    {
        return $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=";
    }

    private static IEnumerable<string> Base64Split(byte[] data, int chunkSize)
    {
        string base64 = Convert.ToBase64String(data);
        for (int i = 0; i < base64.Length; i += chunkSize)
        {
            yield return base64.Substring(i, Math.Min(chunkSize, base64.Length - i));
        }
    }
}
