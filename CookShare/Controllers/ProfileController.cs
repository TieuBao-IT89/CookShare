using Microsoft.AspNetCore.Mvc;

namespace CookShare.Controllers
{
    public class ProfileController : Controller
    {
        public IActionResult Index(string? userId = null)
        {
            ViewData["TargetUserId"] = userId ?? string.Empty;
            return View();
        }
    }
}
