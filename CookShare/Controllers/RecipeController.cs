using Microsoft.AspNetCore.Mvc;

namespace CookShare.Controllers
{
    public class RecipeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Edit(int id)
        {
            ViewBag.RecipeId = id;
            return View();
        }

        public IActionResult Details(int id)
        {
            ViewBag.RecipeId = id;
            return View();
        }
    }
}