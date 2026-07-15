using Application.Interfaces.IServices;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Application.Services
{
    /// <summary>
    /// Gửi email thật qua Gmail SMTP.
    /// Cần bật "2-Step Verification" và tạo "App Password" trong tài khoản Google.
    /// Cấu hình trong appsettings.json > "Email" section.
    /// </summary>
    public class GmailEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public GmailEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var senderEmail = _configuration["Email:SenderEmail"]
                ?? throw new InvalidOperationException("Email:SenderEmail chưa được cấu hình.");
            var senderName = _configuration["Email:SenderName"] ?? "BIM CDE Portal";
            var appPassword = _configuration["Email:AppPassword"]
                ?? throw new InvalidOperationException("Email:AppPassword chưa được cấu hình.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            // Build HTML email body
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = BuildHtmlEmail(subject, body),
                TextBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Gmail SMTP: smtp.gmail.com:465 với SSL
            await client.ConnectAsync("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(senderEmail, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private static string BuildHtmlEmail(string subject, string plainText)
        {
            // Chuyển newlines thành <br> cho HTML
            var htmlBody = plainText
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\n", "<br>");

            return $"""
                <!DOCTYPE html>
                <html lang="vi">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  <title>{subject}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f4f4f0;font-family:'Segoe UI',Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f0;padding:40px 0;">
                    <tr>
                      <td align="center">
                        <table width="560" cellpadding="0" cellspacing="0" style="background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.06);">
                          
                          <!-- Header -->
                          <tr>
                            <td style="background-color:#406623;padding:32px 40px;text-align:center;">
                              <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:-0.3px;">
                                🌿 BIM CDE Portal
                              </h1>
                            </td>
                          </tr>
                          
                          <!-- Body -->
                          <tr>
                            <td style="padding:40px 40px 32px;color:#1B1C17;font-size:15px;line-height:1.6;">
                              <h2 style="margin:0 0 20px;font-size:20px;font-weight:600;color:#1B1C17;">{subject}</h2>
                              <p style="margin:0;color:#43493C;">{htmlBody}</p>
                            </td>
                          </tr>

                          <!-- Footer -->
                          <tr>
                            <td style="padding:24px 40px;background-color:#f8f8f5;border-top:1px solid #E4E3DB;text-align:center;">
                              <p style="margin:0;font-size:12px;color:#73796B;">
                                Email này được gửi tự động từ hệ thống BIM CDE Portal.<br>
                                Vui lòng không trả lời email này.
                              </p>
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
