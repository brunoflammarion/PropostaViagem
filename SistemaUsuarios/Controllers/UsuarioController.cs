using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using BCrypt.Net;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado()
        {
            return HttpContext.Session.GetString("UsuarioId") != null;
        }

        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarios = await _context.Usuarios.ToListAsync();
            return View(usuarios);
        }

        [HttpGet]
        public IActionResult Cadastrar()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Cadastrar(UsuarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Limpar formatação antes de salvar
            string cpfLimpo = LimparCPF(model.CPF);
            string telefoneLimpo = LimparTelefone(model.Telefone);

            // Verificar se email já existe
            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Este email já está em uso");
                return View(model);
            }

            // Verificar se CPF já existe
            if (await _context.Usuarios.AnyAsync(u => u.CPF == cpfLimpo))
            {
                ModelState.AddModelError("CPF", "Este CPF já está em uso");
                return View(model);
            }

            // Verificar se telefone já existe
            if (await _context.Usuarios.AnyAsync(u => u.Telefone == telefoneLimpo))
            {
                ModelState.AddModelError("Telefone", "Este telefone já está em uso");
                return View(model);
            }

            var usuario = new Usuario
            {
                Nome = model.Nome.Trim(),
                Email = model.Email.ToLower().Trim(),
                Telefone = telefoneLimpo,
                CPF = cpfLimpo,
                Senha = BCrypt.Net.BCrypt.HashPassword(model.Senha),
                DataCriacao = DateTime.Now,
                Status = StatusUsuario.Novo
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Usuário cadastrado com sucesso!";
            return RedirectToAction("Login", "Auth");
        }

        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }

            var model = new UsuarioViewModel
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Telefone = FormatarTelefone(usuario.Telefone),
                CPF = FormatarCPF(usuario.CPF),
                Status = usuario.Status,
                DataCriacao = usuario.DataCriacao
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(UsuarioViewModel model)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            ModelState.Remove("Senha");
            ModelState.Remove("ConfirmarSenha");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = await _context.Usuarios.FindAsync(model.Id);
            if (usuario == null)
            {
                return NotFound();
            }

            // Limpar formatação antes de salvar
            string cpfLimpo = LimparCPF(model.CPF);
            string telefoneLimpo = LimparTelefone(model.Telefone);

            // Verificar se email já existe (exceto o próprio usuário)
            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower() && u.Id != model.Id))
            {
                ModelState.AddModelError("Email", "Este email já está em uso");
                return View(model);
            }

            // Verificar se CPF já existe (exceto o próprio usuário)
            if (await _context.Usuarios.AnyAsync(u => u.CPF == cpfLimpo && u.Id != model.Id))
            {
                ModelState.AddModelError("CPF", "Este CPF já está em uso");
                return View(model);
            }

            // Verificar se telefone já existe (exceto o próprio usuário)
            if (await _context.Usuarios.AnyAsync(u => u.Telefone == telefoneLimpo && u.Id != model.Id))
            {
                ModelState.AddModelError("Telefone", "Este telefone já está em uso");
                return View(model);
            }

            usuario.Nome = model.Nome.Trim();
            usuario.Email = model.Email.ToLower().Trim();
            usuario.Telefone = telefoneLimpo;
            usuario.CPF = cpfLimpo;
            usuario.Status = model.Status;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Usuário atualizado com sucesso!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AlterarStatus(Guid id, StatusUsuario status)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }

            usuario.Status = status;
            await _context.SaveChangesAsync();

            string mensagem = status switch
            {
                StatusUsuario.Ativo => "Usuário ativado com sucesso!",
                StatusUsuario.Inativo => "Usuário inativado com sucesso!",
                StatusUsuario.Bloqueado => "Usuário bloqueado com sucesso!",
                _ => "Status alterado com sucesso!"
            };

            TempData["Sucesso"] = mensagem;
            return RedirectToAction("Index");
        }

        private string LimparCPF(string cpf)
        {
            return Regex.Replace(cpf ?? "", @"[^\d]", "");
        }

        private string LimparTelefone(string telefone)
        {
            return Regex.Replace(telefone ?? "", @"[^\d]", "");
        }

        private string FormatarCPF(string cpf)
        {
            if (string.IsNullOrEmpty(cpf) || cpf.Length != 11)
                return cpf;

            return $"{cpf.Substring(0, 3)}.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-{cpf.Substring(9, 2)}";
        }

        private string FormatarTelefone(string telefone)
        {
            if (string.IsNullOrEmpty(telefone))
                return telefone;

            if (telefone.Length == 11)
                return $"({telefone.Substring(0, 2)}) {telefone.Substring(2, 5)}-{telefone.Substring(7, 4)}";
            else if (telefone.Length == 10)
                return $"({telefone.Substring(0, 2)}) {telefone.Substring(2, 4)}-{telefone.Substring(6, 4)}";

            return telefone;
        }
    }
}