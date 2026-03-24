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

        public async Task SendEmailConfirmationAsync(string email, string fullName, string confirmationLink, CancellationToken cancellationToken = default)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress, _smtpSettings.FromName),
                Subject = "Confirm your AccSys email",
                IsBodyHtml = true,
                Body = BuildEmailConfirmationBody(fullName, confirmationLink)
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
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Reset your AccSys password</title>
                </head>
                <body style="margin: 0; padding: 0; background-color: #f8fafc; font-family: Arial, 'Helvetica Neue', Helvetica, sans-serif; -webkit-font-smoothing: antialiased; color: #0f172a;">
                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color: #f8fafc; padding: 40px 20px;">
                        <tr>
                            <td align="center">
                                <table width="600" cellpadding="0" cellspacing="0" border="0" style="max-width: 600px; width: 100%; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
                                    
                                    <tr>
                                        <td align="center" style="padding: 40px 40px 20px 40px; border-bottom: 1px solid #f1f5f9;">
                                            <img src="https://ik.imagekit.io/t1ps8g845/AccsysLogo.png" alt="AccSys Logo" height="45" style="display: block; border: 0; outline: none; text-decoration: none;" />
                                        </td>
                                    </tr>
                                    
                                    <tr>
                                        <td style="padding: 40px;">
                                            <h1 style="margin: 0 0 20px 0; font-size: 24px; font-weight: bold; color: #0f172a; letter-spacing: -0.5px;">Reset your password</h1>
                                            
                                            <p style="margin: 0 0 16px 0; font-size: 16px; line-height: 1.5; color: #334155;">Hello {safeName},</p>
                                            
                                            <p style="margin: 0 0 32px 0; font-size: 16px; line-height: 1.6; color: #475569;">We received a request to reset the password for your AccSys account. If you made this request, please click the button below to securely set a new password.</p>
                                            
                                            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom: 32px;">
                                                <tr>
                                                    <td align="left">
                                                        <table cellpadding="0" cellspacing="0" border="0">
                                                            <tr>
                                                                <td align="center" bgcolor="#0f172a" style="border-radius: 6px;">
                                                                    <a href="{safeLink}" target="_blank" style="display: inline-block; padding: 14px 28px; font-size: 16px; font-weight: bold; color: #ffffff; text-decoration: none; border-radius: 6px; background-color: #0f172a; border: 1px solid #0f172a;">Reset Password</a>
                                                                </td>
                                                            </tr>
                                                        </table>
                                                    </td>
                                                </tr>
                                            </table>

                                            <p style="margin: 0 0 8px 0; font-size: 14px; color: #64748b;">If the button doesn't work, copy and paste this link into your browser:</p>
                                            <p style="margin: 0 0 32px 0; font-size: 14px; word-break: break-all; line-height: 1.5;">
                                                <a href="{safeLink}" target="_blank" style="color: #2563eb; text-decoration: none;">{safeLink}</a>
                                            </p>

                                            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px;">
                                                <tr>
                                                    <td style="padding: 16px; font-size: 13px; line-height: 1.5; color: #64748b;">
                                                        <strong style="color: #334155;">Security Notice:</strong> This link will expire shortly. If you did not request a password reset, no further action is required and you can safely ignore this email.
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>

                                    <tr>
                                        <td style="background-color: #f8fafc; border-top: 1px solid #e2e8f0; padding: 24px 40px; text-align: center;">
                                            <p style="margin: 0 0 8px 0; font-size: 14px; font-weight: bold; color: #334155;">AccSys Solutions</p>
                                            <p style="margin: 0; font-size: 12px; color: #94a3b8;">This is an automated message. Please do not reply to this email.</p>
                                        </td>
                                    </tr>

                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """;
        }

        private static string BuildEmailConfirmationBody(string fullName, string confirmationLink)
        {
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var safeLink = WebUtility.HtmlEncode(confirmationLink);

            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Confirm your AccSys email</title>
                </head>
                <body style="margin: 0; padding: 0; background-color: #f8fafc; font-family: Arial, 'Helvetica Neue', Helvetica, sans-serif; -webkit-font-smoothing: antialiased; color: #0f172a;">
                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color: #f8fafc; padding: 40px 20px;">
                        <tr>
                            <td align="center">
                                <table width="600" cellpadding="0" cellspacing="0" border="0" style="max-width: 600px; width: 100%; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
                                    
                                    <tr>
                                        <td align="center" style="padding: 40px 40px 20px 40px; border-bottom: 1px solid #f1f5f9;">
                                            <img src="https://ik.imagekit.io/t1ps8g845/AccsysLogo.png" alt="AccSys Logo" height="45" style="display: block; border: 0; outline: none; text-decoration: none;" />
                                        </td>
                                    </tr>
                                    
                                    <tr>
                                        <td style="padding: 40px;">
                                            <h1 style="margin: 0 0 20px 0; font-size: 24px; font-weight: bold; color: #0f172a; letter-spacing: -0.5px;">Confirm your email address</h1>
                                            
                                            <p style="margin: 0 0 16px 0; font-size: 16px; line-height: 1.5; color: #334155;">Welcome {safeName},</p>
                                            
                                            <p style="margin: 0 0 32px 0; font-size: 16px; line-height: 1.6; color: #475569;">Thank you for registering with AccSys. To complete your setup and ensure the security of your account, please confirm your email address by clicking the button below.</p>
                                            
                                            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom: 32px;">
                                                <tr>
                                                    <td align="left">
                                                        <table cellpadding="0" cellspacing="0" border="0">
                                                            <tr>
                                                                <td align="center" bgcolor="#0f172a" style="border-radius: 6px;">
                                                                    <a href="{safeLink}" target="_blank" style="display: inline-block; padding: 14px 28px; font-size: 16px; font-weight: bold; color: #ffffff; text-decoration: none; border-radius: 6px; background-color: #0f172a; border: 1px solid #0f172a;">Confirm Email Address</a>
                                                                </td>
                                                            </tr>
                                                        </table>
                                                    </td>
                                                </tr>
                                            </table>

                                            <p style="margin: 0 0 8px 0; font-size: 14px; color: #64748b;">If the button doesn't work, copy and paste this link into your browser:</p>
                                            <p style="margin: 0 0 32px 0; font-size: 14px; word-break: break-all; line-height: 1.5;">
                                                <a href="{safeLink}" target="_blank" style="color: #2563eb; text-decoration: none;">{safeLink}</a>
                                            </p>

                                            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px;">
                                                <tr>
                                                    <td style="padding: 16px; font-size: 13px; line-height: 1.5; color: #64748b;">
                                                        <strong style="color: #334155;">Security Notice:</strong> If you did not sign up for an AccSys account, please ignore this email. Your email address will not be used without confirmation.
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>

                                    <tr>
                                        <td style="background-color: #f8fafc; border-top: 1px solid #e2e8f0; padding: 24px 40px; text-align: center;">
                                            <p style="margin: 0 0 8px 0; font-size: 14px; font-weight: bold; color: #334155;">AccSys Solutions</p>
                                            <p style="margin: 0; font-size: 12px; color: #94a3b8;">This is an automated message. Please do not reply to this email.</p>
                                        </td>
                                    </tr>

                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """;
        }
    }
}