using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using CookShare.Data.Seeding;
using CookShare.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CookShare.Tests.Seeding
{
    public sealed class TheMealDbTests
    {
        private const string MealJson = """
            {
              "idMeal": "52772",
              "strMeal": "Teriyaki Chicken Casserole",
              "strCategory": "Chicken",
              "strArea": "Japanese",
              "strMealThumb": "https://www.themealdb.com/images/media/meals/test.jpg",
              "strInstructions": "Prepare the ingredients.\r\nCook the chicken thoroughly.\r\nServe.",
              "strIngredient1": " Chicken ", "strMeasure1": " 500 g ",
              "strIngredient2": "Rice", "strMeasure2": "",
              "strIngredient3": "   ", "strMeasure3": "1 cup",
              "strIngredient4": null, "strMeasure4": null,
              "strIngredient20": "Soy sauce", "strMeasure20": "1 tbsp",
              "strSource": "https://example.com/original-recipe"
            }
            """;

        [Fact]
        public void Mapper_ShouldKeepSourceInformationAndCombineAllTwentyIngredientSlots()
        {
            var meal = JsonSerializer.Deserialize<TheMealDbMeal>(MealJson)!;

            var result = TheMealDbMapper.Map(meal);

            Assert.Equal("52772", result.Key);
            Assert.Equal("Teriyaki Chicken Casserole", result.Name);
            Assert.Equal("Món chính", result.Category);
            Assert.Contains("Japanese", result.Description);
            Assert.Equal("500 g Chicken\nRice\n1 tbsp Soy sauce", result.Ingredients);
            Assert.Equal("Prepare the ingredients.\nCook the chicken thoroughly.\nServe.", result.Instructions);
            Assert.Equal("https://example.com/original-recipe", result.SourceUrl);
            Assert.Empty(result.Validate());
        }

        [Theory]
        [InlineData("Dessert", "Món tráng miệng")]
        [InlineData("Side", "Món phụ")]
        [InlineData("Starter", "Món phụ")]
        [InlineData("Vegan", "Món chay")]
        [InlineData("Vegetarian", "Món chay")]
        [InlineData("Breakfast", "Ăn sáng")]
        [InlineData("Seafood", "Món chính")]
        public void Mapper_ShouldUseSupportedCookShareCategories(string category, string expected)
        {
            var meal = JsonSerializer.Deserialize<TheMealDbMeal>(MealJson)!;
            meal.Category = category;

            Assert.Equal(expected, TheMealDbMapper.Map(meal).Category);
        }

        [Theory]
        [InlineData(null, "Unknown")]
        [InlineData("", "Chicken")]
        [InlineData("../x", "Chicken")]
        [InlineData("52772", "Unknown")]
        public void Mapper_ShouldRejectMissingIdentityOrUnknownCategory(string? id, string category)
        {
            var meal = JsonSerializer.Deserialize<TheMealDbMeal>(MealJson)!;
            meal.Id = id;
            meal.Category = category;

            Assert.Throws<ValidationException>(() => TheMealDbMapper.Map(meal));
        }

        [Fact]
        public void Mapper_ShouldReplaceUnsafeUrlsAndReportEmptyRequiredFields()
        {
            var meal = JsonSerializer.Deserialize<TheMealDbMeal>(MealJson)!;
            meal.Image = "javascript:alert(1)";
            meal.SourceUrl = "https://user:password@example.com/secret";
            meal.Name = " ";
            meal.Instructions = null;
            meal.Fields.Clear();

            var result = TheMealDbMapper.Map(meal);

            Assert.Equal(RecipeImageReference.Placeholder, result.Image);
            Assert.Equal("https://www.themealdb.com/meal.php?c=52772", result.SourceUrl);
            Assert.True(result.Validate().Count >= 3);
        }

        [Fact]
        public async Task Client_ShouldEncodeSearchUseOnlyV1AndLimitDistinctCandidates()
        {
            using var handler = new StubMealHandler(_ => JsonResponse($$"""{"meals":[{{MealJson}},{{MealJson}},{{MealJson.Replace("52772", "52773", StringComparison.Ordinal)}}]}"""));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions
            {
                Source = "themealdb",
                Search = "chicken & rice",
                Count = 2
            });

            Assert.Equal(new[] { "52772", "52773" }, result.Recipes.Select(recipe => recipe.Key));
            Assert.Empty(result.Errors);
            Assert.Equal("https://www.themealdb.com/api/json/v1/1/search.php?s=chicken%20%26%20rice", Assert.Single(handler.Requests).AbsoluteUri);
        }

        [Fact]
        public async Task Client_Lookup_ShouldPreserveIdentityAndBoundResultCount()
        {
            using var handler = new StubMealHandler(_ => JsonResponse($$"""{"meals":[{{MealJson}}]}"""));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions { Source = "themealdb", MealId = "52772", Count = 1 });

            Assert.Equal("52772", Assert.Single(result.Recipes).Key);
            Assert.EndsWith("/lookup.php?i=52772", Assert.Single(handler.Requests).AbsoluteUri);
        }

        [Fact]
        public async Task Client_DefaultDiscovery_ShouldStopAfterEnoughCandidates()
        {
            using var handler = new StubMealHandler(_ => JsonResponse($$"""{"meals":[{{MealJson}}]}"""));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions { Source = "themealdb", Count = 1 });

            Assert.Single(result.Recipes);
            Assert.EndsWith("search.php?f=a", Assert.Single(handler.Requests).AbsoluteUri);
        }

        [Fact]
        public async Task Client_DefaultDiscovery_ShouldBeBoundedEvenWhenEveryResponseIsEmpty()
        {
            using var handler = new StubMealHandler(_ => JsonResponse("{\"meals\":null}"));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions { Source = "themealdb" });

            Assert.Empty(result.Recipes);
            Assert.Equal(26, handler.Requests.Count);
            Assert.EndsWith("search.php?f=z", handler.Requests[^1].AbsoluteUri);
        }

        [Theory]
        [InlineData("{\"meals\":null}")]
        [InlineData("{\"meals\":[]}")]
        public async Task Client_ShouldAcceptEmptySearchResults(string response)
        {
            using var handler = new StubMealHandler(_ => JsonResponse(response));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions { Source = "themealdb", Search = "no-match" });

            Assert.Empty(result.Recipes);
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"meals\":{}}")]
        [InlineData("[]")]
        [InlineData("not json")]
        public async Task Client_ShouldRejectInvalidResponseEnvelopes(string response)
        {
            using var handler = new StubMealHandler(_ => JsonResponse(response));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            await Assert.ThrowsAnyAsync<JsonException>(() => client.ReadAsync(new RecipeSeedOptions { Source = "themealdb", Search = "test" }));
        }

        [Fact]
        public async Task Client_ShouldReportMalformedMealsAndContinueWithValidOnes()
        {
            using var handler = new StubMealHandler(_ => JsonResponse($$"""{"meals":[null,{},{{MealJson}}]}"""));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            var result = await client.ReadAsync(new RecipeSeedOptions { Source = "themealdb", Search = "test" });

            Assert.Single(result.Recipes);
            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public async Task Client_ShouldPropagateHttpErrorsWithoutReturningPartialData()
        {
            using var handler = new StubMealHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            await Assert.ThrowsAsync<HttpRequestException>(() => client.ReadAsync(new RecipeSeedOptions { Source = "themealdb" }));
        }

        [Fact]
        public async Task Client_ShouldHonorRequestTimeout()
        {
            using var handler = new DelayedMealHandler();
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(30) };
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync(new RecipeSeedOptions { Source = "themealdb" }));
        }

        [Fact]
        public async Task Import_ShouldPersistMappedApiRecipesAndSkipThemOnRerun()
        {
            await using var db = RecipeSeedTestSupport.CreateDb();
            await RecipeSeedTestSupport.AddUsersAsync(db);
            using var handler = new StubMealHandler(_ => JsonResponse($$"""{"meals":[{{MealJson}}]}"""));
            using var http = new HttpClient(handler);
            var client = new TheMealDbClient(http, new ConfigurationBuilder().Build());
            var seeder = RecipeSeedTestSupport.Seeder(db, client);
            var options = new RecipeSeedOptions { Source = "themealdb", MealId = "52772", Count = 1 };

            var first = await seeder.RunAsync(options);
            var repeated = await seeder.RunAsync(options);

            Assert.Equal(1, first.Added);
            Assert.Equal(1, repeated.Skipped);
            var stored = await db.Recipes.Include(recipe => recipe.User).SingleAsync();
            Assert.Equal("themealdb", stored.Source);
            Assert.Equal("52772", stored.ExternalId);
            Assert.Equal("admintest1", stored.User!.UserName);
            Assert.Equal("https://example.com/original-recipe", stored.SourceUrl);
            Assert.Equal("500 g Chicken\nRice\n1 tbsp Soy sauce", stored.Ingredients);
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        private sealed class StubMealHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<Uri> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!);
                return Task.FromResult(respond(request));
            }
        }

        private sealed class DelayedMealHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return JsonResponse("{\"meals\":null}");
            }
        }
    }
}
