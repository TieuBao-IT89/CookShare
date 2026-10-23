using CookShare.Models;

namespace CookShare.Data
{
    public static class RecipeQueryExtensions
    {
        // Parameterless ToLower translates to PostgreSQL lower(); the culture and
        // StringComparison overloads cannot be translated by this EF provider.
#pragma warning disable CA1304, CA1311, CA1862
        public static IQueryable<Recipe> FilterBySearchAndCategory(
            this IQueryable<Recipe> recipes, string query, string category)
        {
            if (!string.IsNullOrEmpty(query))
            {
                string searchTerm = query.ToLowerInvariant();
                recipes = recipes.Where(r => r.Name.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(category))
            {
                string categoryTerm = category.ToLowerInvariant();
                recipes = recipes.Where(r => r.Category.ToLower() == categoryTerm);
            }

            return recipes;
        }
#pragma warning restore CA1304, CA1311, CA1862
    }
}
