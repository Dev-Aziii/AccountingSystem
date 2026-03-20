using AccountingSystem.API.Configuration;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AccountingSystem.API.Services
{
    public class SmtpAccountEmailService : IAccountEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public SmtpAccountEmailService(IOptions<SmtpSettings> smtpOptions)
        {
            _smtpSettings = smtpOptions.Value;
        }

        public async Task SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress, _smtpSettings.FromName),
                Subject = "Reset your AccSys password",
                IsBodyHtml = true,
                Body = BuildPasswordResetBody(fullName, resetLink)
            };

            message.To.Add(new MailAddress(email, fullName));

            using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
            {
                EnableSsl = _smtpSettings.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password)
            };

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);
        }

        private static string BuildPasswordResetBody(string fullName, string resetLink)
        {
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var safeLink = WebUtility.HtmlEncode(resetLink);

            return $"""
                <html>
                <body style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
                    <p>Hello {safeName},</p>
                    <p>We received a request to reset your AccSys password.</p>
                    <p>
                        <a href="{safeLink}" style="display:inline-block;padding:10px 16px;background:#134658;color:#ffffff;text-decoration:none;border-radius:6px;">
                            Reset Password
                        </a>
                    </p>
                    <p>If you did not request this change, you can ignore this email.</p>
                    <p>This link expires based on the server token policy.</p>
                </body>
                </html>
                """;
        }
    }
}
