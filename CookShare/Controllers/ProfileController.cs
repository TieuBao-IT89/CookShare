using Microsoft.AspNetCore.Mvc;

namespace CookShare.Controllers
{
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}