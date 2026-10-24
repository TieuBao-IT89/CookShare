using System.Net;
using System.Net.Sockets;
using System.Text;
using CookShare.Options;
using CookShare.Services;
using MailKit.Security;

namespace CookShare.Tests.Services
{
    public class SmtpAccountEmailSenderTests
    {
        [Fact]
        public async Task StartTls_requires_encryption_before_credentials_or_message_are_sent()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var conversation = new List<string>();
            var server = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                await using var stream = socket.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };
                await writer.WriteLineAsync("220 localhost ESMTP");
                var hello = await reader.ReadLineAsync(timeout.Token);
                conversation.Add(hello ?? string.Empty);
                await writer.WriteLineAsync("250-localhost\r\n250 AUTH PLAIN LOGIN");
                var following = await reader.ReadLineAsync(timeout.Token);
                if (following != null)
                {
                    conversation.Add(following);
                }
            }, timeout.Token);
            var sender = Sender(port, useStartTls: true);

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                sender.SendAsync("recipient@example.com", "Subject", "Body", timeout.Token));
            await server;

            Assert.Contains(conversation, line => line.StartsWith("EHLO ", StringComparison.Ordinal));
            Assert.DoesNotContain(conversation, line =>
                line.StartsWith("AUTH ", StringComparison.Ordinal) ||
                line.StartsWith("MAIL ", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Disabling_StartTls_requires_implicit_TLS_instead_of_cleartext_SMTP()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                await using var stream = socket.GetStream();
                var firstByte = new byte[1];
                var count = await stream.ReadAsync(firstByte, timeout.Token);
                return count == 1 ? firstByte[0] : -1;
            }, timeout.Token);
            var sender = Sender(port, useStartTls: false);

            await Assert.ThrowsAsync<SslHandshakeException>(() =>
                sender.SendAsync("recipient@example.com", "Subject", "Body", timeout.Token));

            // A TLS ClientHello starts with 0x16. Some restricted Windows hosts
            // cannot acquire SChannel credentials and abort before sending it.
            // Both paths must fail the TLS handshake without sending SMTP text.
            var firstByte = await server;
            Assert.True(firstByte is -1 or 0x16);
        }

        private static SmtpAccountEmailSender Sender(int port, bool useStartTls)
        {
            return new SmtpAccountEmailSender(Microsoft.Extensions.Options.Options.Create(new EmailOptions
            {
                SmtpHost = "127.0.0.1",
                SmtpPort = port,
                FromAddress = "sender@example.com",
                SmtpUser = "smtp-user",
                SmtpPassword = "test-password",
                UseStartTls = useStartTls,
                TimeoutSeconds = 3
            }));
        }
    }
}
