using System.ComponentModel.DataAnnotations;

namespace CookShare.Models
{
    public enum RecipeDifficulty
    {
        [Display(Name = "Dễ")]
        Easy = 1,

        [Display(Name = "Trung bình")]
        Medium = 2,

        [Display(Name = "Khó")]
        Hard = 3
    }

    public static class RecipeDifficultyExtensions
    {
        public static string GetDisplayName(this RecipeDifficulty difficulty)
        {
            return difficulty switch
            {
                RecipeDifficulty.Easy => "Dễ",
                RecipeDifficulty.Medium => "Trung bình",
                RecipeDifficulty.Hard => "Khó",
                _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
            };
        }
    }
}
