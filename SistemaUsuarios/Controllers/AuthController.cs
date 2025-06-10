using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using BCrypt.Net;

namespace SistemaUsuarios.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(model.Senha, usuario.Senha))
            {
                ModelState.AddModelError("", "Email ou senha inválidos");
                return View(model);
            }

            if (usuario.Status == StatusUsuario.Bloqueado)
            {
                ModelState.AddModelError("", "Usuário bloqueado");
                return View(model);
            }

            if (usuario.Status == StatusUsuario.Inativo)
            {
                ModelState.AddModelError("", "Usuário inativo");
                return View(model);
            }

            // Criar sessão
            HttpContext.Session.SetString("UsuarioId", usuario.Id.ToString());
            HttpContext.Session.SetString("UsuarioNome", usuario.Nome);

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}