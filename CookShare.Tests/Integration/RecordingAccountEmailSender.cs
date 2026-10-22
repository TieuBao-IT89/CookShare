using CookShare.Services;

namespace CookShare.Tests.Integration
{
    public sealed class RecordingAccountEmailSender
        : IAccountEmailSender
    {
        public List<RecordedAccountEmail> Messages { get; } = new();

        public Task SendAsync(
            string email,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new RecordedAccountEmail(
                email, subject, htmlBody));

            return Task.CompletedTask;
        }
    }

    public sealed record RecordedAccountEmail(
        string Email,
        string Subject,
        string HtmlBody);
}
