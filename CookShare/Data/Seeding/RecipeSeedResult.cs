namespace CookShare.Data.Seeding
{
    public sealed class RecipeSeedResult
    {
        public int Added { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public int WouldAdd { get; set; }
        public List<string> Errors { get; } = new();
        public List<RecipeSeedPreview> Preview { get; } = new();
    }

    public sealed record RecipeSeedPreview(string UserName, RecipeSeedRecord Recipe);
}
