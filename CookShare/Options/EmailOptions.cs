namespace CookShare.Options
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public string PublicBaseUrl { get; set; } = string.Empty;

        public string FromAddress { get; set; } = string.Empty;

        public string FromName { get; set; } = "CookShare";

        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        public string SmtpUser { get; set; } = string.Empty;

        public string SmtpPassword { get; set; } = string.Empty;

        public bool UseStartTls { get; set; } = true;

        public int TimeoutSeconds { get; set; } = 30;
    }
}
