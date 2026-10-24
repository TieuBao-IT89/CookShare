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
                string.IsNullOrWhiteSpace(_options.FromAddress) ||
                _options.SmtpPort < 1 || _options.SmtpPort > 65535 ||
                _options.TimeoutSeconds < 1 || _options.TimeoutSeconds > 120)
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

            using var deliveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deliveryTimeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            cancellationToken = deliveryTimeout.Token;

            using var client = new SmtpClient
            {
                Timeout = _options.TimeoutSeconds * 1000
            };
            var socket = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.SslOnConnect;

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
