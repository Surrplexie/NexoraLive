namespace NL.Identity;

public interface IEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default);
}

/// <summary>Logs verification codes to audit — default for local dev.</summary>
public sealed class MockEmailSender : IEmailSender
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NL verification] Email to {email}: code {code}");
        return Task.CompletedTask;
    }
}

/// <summary>Optional SMTP sender when NL_EMAIL_SMTP_HOST is configured.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var host = Environment.GetEnvironmentVariable("NL_EMAIL_SMTP_HOST");
        var portRaw = Environment.GetEnvironmentVariable("NL_EMAIL_SMTP_PORT");
        var user = Environment.GetEnvironmentVariable("NL_EMAIL_SMTP_USER");
        var password = Environment.GetEnvironmentVariable("NL_EMAIL_SMTP_PASSWORD");
        var from = Environment.GetEnvironmentVariable("NL_EMAIL_FROM") ?? user;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("NL_EMAIL_SMTP_HOST and NL_EMAIL_FROM required for SMTP mode.");
        }

        var port = int.TryParse(portRaw, out var p) ? p : 587;
        using var client = new System.Net.Mail.SmtpClient(host.Trim(), port)
        {
            EnableSsl = !string.Equals(Environment.GetEnvironmentVariable("NL_EMAIL_SMTP_TLS"), "0", StringComparison.OrdinalIgnoreCase),
        };

        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new System.Net.NetworkCredential(user.Trim(), password);
        }

        using var message = new System.Net.Mail.MailMessage(from.Trim(), email.Trim())
        {
            Subject = "Your NexoraLive verification code",
            Body = $"Your verification code is: {code}\n\nIt expires in 10 minutes.",
            IsBodyHtml = false,
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
