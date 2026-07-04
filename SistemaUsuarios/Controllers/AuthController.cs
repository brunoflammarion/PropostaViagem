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
            HttpContext.Session.SetString("TipoUsuario", usuario.TipoUsuario.ToString());
            if (usuario.UsuarioMasterId.HasValue)
                HttpContext.Session.SetString("UsuarioMasterId", usuario.UsuarioMasterId.Value.ToString());
            if (!string.IsNullOrEmpty(usuario.FotoPath))
                HttpContext.Session.SetString("FotoPath", usuario.FotoPath);
            if (!string.IsNullOrEmpty(usuario.CorPrimaria))
                HttpContext.Session.SetString("CorPrimaria", usuario.CorPrimaria);
            if (!string.IsNullOrEmpty(usuario.CorSecundaria))
                HttpContext.Session.SetString("CorSecundaria", usuario.CorSecundaria);
            if (!string.IsNullOrEmpty(usuario.CorDestaque))
                HttpContext.Session.SetString("CorDestaque", usuario.CorDestaque);

            // Resolver nome do master para Associados (exibição no layout)
            if (usuario.TipoUsuario == TipoUsuario.Associado && usuario.UsuarioMasterId.HasValue)
            {
                var master = await _context.Usuarios
                    .AsNoTracking()
                    .Select(u => new { u.Id, u.Nome })
                    .FirstOrDefaultAsync(u => u.Id == usuario.UsuarioMasterId.Value);
                if (master != null)
                    HttpContext.Session.SetString("NomeMaster", master.Nome);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}