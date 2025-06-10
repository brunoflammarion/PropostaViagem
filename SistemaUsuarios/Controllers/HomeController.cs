using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace SistemaUsuarios.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioId") == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}