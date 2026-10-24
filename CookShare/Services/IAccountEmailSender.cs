namespace CookShare.Services
{
    public interface IAccountEmailSender
    {
        Task SendAsync(
            string email,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default);
    }
}
