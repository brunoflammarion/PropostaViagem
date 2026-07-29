using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using SistemaUsuarios.Services.Email;
using BCrypt.Net;
using System.Security.Cryptography;

namespace SistemaUsuarios.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext  _context;
        private readonly IEmailService         _emailService;
        private readonly EmailTemplateService  _templateService;

        public AuthController(
            ApplicationDbContext context,
            IEmailService emailService,
            EmailTemplateService templateService)
        {
            _context         = context;
            _emailService    = emailService;
            _templateService = templateService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

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

            if (usuario.TipoUsuario == TipoUsuario.Associado && usuario.UsuarioMasterId.HasValue)
            {
                var master = await _context.Usuarios
                    .AsNoTracking()
                    .Select(u => new { u.Id, u.Nome })
                    .FirstOrDefaultAsync(u => u.Id == usuario.UsuarioMasterId.Value);
                if (master != null)
                    HttpContext.Session.SetString("NomeMaster", master.Nome);
            }

            TempData["AnalyticsEvents"] = System.Text.Json.JsonSerializer.Serialize(new object[] {
                new { name = "login", parameters = new { method = "email" } }
            });
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ── RESET DE SENHA ────────────────────────────────────────────────────────

        // GET /Auth/EsqueceuSenha
        [HttpGet]
        public IActionResult EsqueceuSenha() => View();

        // POST /Auth/EsqueceuSenha
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EsqueceuSenha(string email)
        {
            // Sempre exibe a mesma mensagem — evita enumeração de e-mails
            TempData["ResetMensagem"] = "Se esse e-mail estiver cadastrado, você receberá as instruções em breve.";

            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(EsqueceuSenha));

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower());

            if (usuario == null)
                return RedirectToAction(nameof(EsqueceuSenha));

            // Invalida tokens anteriores ainda ativos
            var tokensAtivos = await _context.PasswordResetTokens
                .Where(t => t.UsuarioId == usuario.Id && !t.Usado && t.Expiry > DateTime.UtcNow)
                .ToListAsync();
            foreach (var t in tokensAtivos)
                t.Usado = true;

            // Gera token criptograficamente seguro
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UsuarioId = usuario.Id,
                Token     = token,
                Expiry    = DateTime.UtcNow.AddHours(1),
            });
            await _context.SaveChangesAsync();

            var resetUrl = Url.Action("RedefinirSenha", "Auth", new { token }, Request.Scheme)!;
            var html     = _templateService.GetResetSenhaHtml(usuario.Nome, resetUrl);

            try
            {
                await _emailService.SendAsync(
                    usuario.Email, usuario.Nome,
                    "Redefinição de senha • Agent Tools", html);
            }
            catch { /* erro já registrado no SmtpEmailService */ }

            return RedirectToAction(nameof(EsqueceuSenha));
        }

        // GET /Auth/RedefinirSenha?token=...
        [HttpGet]
        public async Task<IActionResult> RedefinirSenha(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(EsqueceuSenha));

            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Usado && t.Expiry > DateTime.UtcNow);

            if (resetToken == null)
            {
                TempData["ResetErro"] = "Link inválido ou expirado. Solicite um novo abaixo.";
                return RedirectToAction(nameof(EsqueceuSenha));
            }

            ViewBag.Token = token;
            return View();
        }

        // POST /Auth/RedefinirSenha
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedefinirSenha(string token, string novaSenha, string confirmarSenha)
        {
            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6 || novaSenha != confirmarSenha)
            {
                ViewBag.Token = token;
                ModelState.AddModelError("", "As senhas não conferem ou têm menos de 6 caracteres.");
                return View();
            }

            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Token == token && !t.Usado && t.Expiry > DateTime.UtcNow);

            if (resetToken == null)
            {
                TempData["ResetErro"] = "Link inválido ou expirado. Solicite um novo abaixo.";
                return RedirectToAction(nameof(EsqueceuSenha));
            }

            resetToken.Usuario.Senha = BCrypt.Net.BCrypt.HashPassword(novaSenha);
            resetToken.Usado         = true;
            await _context.SaveChangesAsync();

            TempData["LoginSucesso"] = "Senha redefinida com sucesso. Faça login com a nova senha.";
            return RedirectToAction("Login");
        }
    }
}
