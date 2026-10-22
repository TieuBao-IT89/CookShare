using CookShare.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CookShare.Services
{
    public class SmtpAccountEmailSender : IAccountEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpAccountEmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(
            string email,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.SmtpHost) ||
                string.IsNullOrWhiteSpace(_options.FromAddress))
            {
                throw new InvalidOperationException(
                    "Email delivery is not configured.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _options.FromName,
                _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using var client = new SmtpClient();
            var socket = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                socket,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            {
                await client.AuthenticateAsync(
                    _options.SmtpUser,
                    _options.SmtpPassword,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
