using System.Net.Mail;
using System.Text;
using BuildServer.Persistence;

namespace BuildServer.Services;

public sealed class EmailNotificationService(
    JsonDatabase database,
    ILogger<EmailNotificationService> logger)
{
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

        string subject = $"[打包成功] {projectName}/{configName} #{job.BuildNumber}";
        string body = BuildNotificationBody(job, projectName, configName);

        foreach (string recipient in job.NotifyEmails.Where(email => !string.IsNullOrWhiteSpace(email)))
        {
            try
            {
                await SendEmailAsync(settings, recipient, subject, body);
                logger.LogInformation("打包成功邮件已发送至 {Recipient}", recipient);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "发送打包成功邮件至 {Recipient} 失败", recipient);
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

    private static string BuildNotificationBody(BuildJobRecord job, string projectName, string configName)
    {
        StringBuilder sb = new();
        sb.AppendLine($"打包成功通知");
        sb.AppendLine();
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
        sb.AppendLine();
        sb.AppendLine("请在 BuildServer 控制台查看详细日志和产物。");
        return sb.ToString();
    }

    private static async Task SendEmailAsync(EmailSettingsRecord settings, string toEmail, string subject, string body)
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
            client.Credentials = new System.Net.NetworkCredential(settings.SmtpUserName, settings.SmtpPassword);
        }

        await client.SendMailAsync(message);
    }
}
