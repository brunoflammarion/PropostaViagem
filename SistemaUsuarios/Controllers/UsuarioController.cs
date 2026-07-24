using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using SistemaUsuarios.Services;
using BCrypt.Net;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blob;

        public UsuarioController(ApplicationDbContext context, BlobStorageService blob)
        {
            _context = context;
            _blob = blob;
        }

        private bool UsuarioLogado()
        {
            return HttpContext.Session.GetString("UsuarioId") != null;
        }

        // GET /Usuario — perfil do usuário logado
        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return RedirectToAction("Logout", "Auth");

            var model = new UsuarioViewModel
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Telefone = FormatarTelefone(usuario.Telefone),
                CPF = FormatarCPF(usuario.CPF),
                Status = usuario.Status,
                DataCriacao = usuario.DataCriacao,
                FotoPath = usuario.FotoPath,
                CorPrimaria   = usuario.CorPrimaria   ?? "#0A1128",
                CorSecundaria = usuario.CorSecundaria ?? "#65a3d4",
                CorDestaque   = usuario.CorDestaque   ?? "#2ec4b6",
                NomeAgencia   = usuario.NomeAgencia,
                SlugAgencia   = usuario.SlugAgencia,
            };

            ViewBag.IsMaster = usuario.TipoUsuario == TipoUsuario.Master;
            if (usuario.TipoUsuario == TipoUsuario.Associado && usuario.UsuarioMasterId.HasValue)
            {
                var master = await _context.Usuarios.FindAsync(usuario.UsuarioMasterId.Value);
                ViewBag.LogoAgenciaMaster = master?.LogoAgenciaPath;
                model.LogoAgenciaPath = master?.LogoAgenciaPath;
            }
            else
            {
                model.LogoAgenciaPath = usuario.LogoAgenciaPath;
            }

            return View(model);
        }

        // POST /Usuario — salvar dados básicos do perfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UsuarioViewModel model)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            // Senha não é alterada neste formulário
            ModelState.Remove("Senha");
            ModelState.Remove("ConfirmarSenha");

            if (!ModelState.IsValid)
                return View(model);

            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return RedirectToAction("Logout", "Auth");

            string cpfLimpo = LimparCPF(model.CPF);
            string telefoneLimpo = LimparTelefone(model.Telefone);

            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower() && u.Id != usuarioId))
            {
                ModelState.AddModelError("Email", "Este e-mail já está em uso por outra conta.");
                return View(model);
            }

            if (await _context.Usuarios.AnyAsync(u => u.CPF == cpfLimpo && u.Id != usuarioId))
            {
                ModelState.AddModelError("CPF", "Este CPF já está em uso por outra conta.");
                return View(model);
            }

            if (await _context.Usuarios.AnyAsync(u => u.Telefone == telefoneLimpo && u.Id != usuarioId))
            {
                ModelState.AddModelError("Telefone", "Este telefone já está em uso por outra conta.");
                return View(model);
            }

            usuario.Nome = model.Nome.Trim();
            usuario.Email = model.Email.ToLower().Trim();
            usuario.Telefone = telefoneLimpo;
            usuario.CPF = cpfLimpo;

            await _context.SaveChangesAsync();

            // Atualiza o nome na sessão
            HttpContext.Session.SetString("UsuarioNome", usuario.Nome);

            TempData["Sucesso"] = "Dados atualizados com sucesso!";
            return RedirectToAction("Index");
        }

        // POST /Usuario/AlterarSenha
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSenha(string senhaAtual, string novaSenha, string confirmarSenha)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                return RedirectToAction("Logout", "Auth");

            if (!BCrypt.Net.BCrypt.Verify(senhaAtual, usuario.Senha))
            {
                TempData["ErraSenha"] = "Senha atual incorreta.";
                return RedirectToAction("Index");
            }

            if (novaSenha.Length < 6)
            {
                TempData["ErraSenha"] = "A nova senha deve ter pelo menos 6 caracteres.";
                return RedirectToAction("Index");
            }

            if (novaSenha != confirmarSenha)
            {
                TempData["ErraSenha"] = "As senhas não conferem.";
                return RedirectToAction("Index");
            }

            usuario.Senha = BCrypt.Net.BCrypt.HashPassword(novaSenha);
            await _context.SaveChangesAsync();

            TempData["SucessoSenha"] = "Senha alterada com sucesso!";
            return RedirectToAction("Index");
        }

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

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

        // POST /Usuario/AlterarFoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarFoto(IFormFile foto)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            if (foto == null || foto.Length == 0)
            {
                TempData["Erro"] = "Selecione uma imagem para fazer upload.";
                return RedirectToAction("Index");
            }

            try
            {
                var usuarioId = ObterUsuarioLogadoId();
                var usuario = await _context.Usuarios.FindAsync(usuarioId);
                if (usuario == null) return RedirectToAction("Logout", "Auth");

                var fotoPath = await SalvarFotoUsuarioAsync(foto);

                // Remove foto anterior se existir
                _ = _blob.DeletarAsync(usuario.FotoPath);

                usuario.FotoPath = fotoPath;
                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("FotoPath", fotoPath);
                TempData["Sucesso"] = "Foto de perfil atualizada!";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Erro"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST /Usuario/AlterarLogoAgencia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarLogoAgencia(IFormFile logo)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var usuario   = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null) return RedirectToAction("Logout", "Auth");

            if (usuario.TipoUsuario != TipoUsuario.Master)
            {
                TempData["ErroLogo"] = "Apenas o usuário master pode alterar a logo da agência.";
                return RedirectToAction("Index");
            }

            if (logo == null || logo.Length == 0)
            {
                TempData["ErroLogo"] = "Selecione uma imagem para fazer upload.";
                return RedirectToAction("Index");
            }

            try
            {
                var logoPath = await _blob.SalvarAsync(logo, "logos-agencia");
                _ = _blob.DeletarAsync(usuario.LogoAgenciaPath);
                usuario.LogoAgenciaPath = logoPath;
                await _context.SaveChangesAsync();
                TempData["SucessoLogo"] = "Logo da agência atualizada!";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErroLogo"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST /Usuario/RemoverLogoAgencia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverLogoAgencia()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var usuario   = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null) return RedirectToAction("Logout", "Auth");

            if (usuario.TipoUsuario != TipoUsuario.Master)
            {
                TempData["ErroLogo"] = "Apenas o usuário master pode remover a logo da agência.";
                return RedirectToAction("Index");
            }

            _ = _blob.DeletarAsync(usuario.LogoAgenciaPath);
            usuario.LogoAgenciaPath = null;
            await _context.SaveChangesAsync();
            TempData["SucessoLogo"] = "Logo da agência removida.";
            return RedirectToAction("Index");
        }

        // POST /Usuario/SalvarIdentidade
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarIdentidade(string corPrimaria, string corSecundaria, string corDestaque, string? nomeAgencia)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var hexRegex = new Regex(@"^#[0-9A-Fa-f]{6}$");
            if (!hexRegex.IsMatch(corPrimaria ?? "") ||
                !hexRegex.IsMatch(corSecundaria ?? "") ||
                !hexRegex.IsMatch(corDestaque ?? ""))
            {
                TempData["ErroIdentidade"] = "Cores inválidas. Use o seletor de cores ou o formato #RRGGBB.";
                return RedirectToAction("Index");
            }

            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null) return RedirectToAction("Logout", "Auth");

            usuario.CorPrimaria   = corPrimaria;
            usuario.CorSecundaria = corSecundaria;
            usuario.CorDestaque   = corDestaque;

            // Atualiza nome e slug da agência
            var nomeNormalizado = (nomeAgencia ?? "").Trim();
            if (!string.IsNullOrEmpty(nomeNormalizado))
            {
                usuario.NomeAgencia = nomeNormalizado;

                // Regenera slug apenas se o nome mudou ou ainda não tem slug
                if (string.IsNullOrEmpty(usuario.SlugAgencia) ||
                    !usuario.NomeAgencia.Equals(nomeNormalizado, StringComparison.OrdinalIgnoreCase))
                {
                    var slugsExistentes = await _context.Usuarios
                        .Where(u => u.SlugAgencia != null)
                        .Select(u => u.SlugAgencia!)
                        .ToListAsync();

                    usuario.SlugAgencia = SlugHelper.GenerateUnique(
                        nomeNormalizado, slugsExistentes,
                        excludeSlug: usuario.SlugAgencia);
                }
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("CorPrimaria",   corPrimaria);
            HttpContext.Session.SetString("CorSecundaria", corSecundaria);
            HttpContext.Session.SetString("CorDestaque",   corDestaque);

            TempData["SucessoIdentidade"] = "Configurações da agência salvas com sucesso!";
            return RedirectToAction("Index");
        }

        private Task<string> SalvarFotoUsuarioAsync(IFormFile foto)
            => _blob.SalvarAsync(foto, "usuarios");

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

        // ── Gestão de Equipe (apenas Master) ──────────────────────────────────────

        private bool IsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        /// <summary>Garante que o id pertence a um associado do master logado. Retorna null se não encontrado.</summary>
        private async Task<Usuario?> ObterAssociadoDoMaster(Guid id)
        {
            var masterId = ObterUsuarioLogadoId();
            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id
                                       && u.UsuarioMasterId == masterId
                                       && u.TipoUsuario == TipoUsuario.Associado);
        }

        // GET /Usuario/GerenciarEquipe
        [HttpGet]
        public async Task<IActionResult> GerenciarEquipe()
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            var masterId = ObterUsuarioLogadoId();
            var associados = await _context.Usuarios
                .Where(u => u.UsuarioMasterId == masterId)
                .OrderBy(u => u.Nome)
                .ToListAsync();

            ViewBag.Associados = associados;
            return View();
        }

        // POST /Usuario/CriarAssociado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarAssociado(string nome, string email, string telefone, string cpf, string senha)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            var masterId = ObterUsuarioLogadoId();
            var cpfLimpo = Regex.Replace(cpf ?? "", @"[^\d]", "");
            var telLimpo = Regex.Replace(telefone ?? "", @"[^\d]", "");

            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            {
                TempData["Erro"] = "Este e-mail já está em uso.";
                return RedirectToAction("GerenciarEquipe");
            }

            if (await _context.Usuarios.AnyAsync(u => u.CPF == cpfLimpo))
            {
                TempData["Erro"] = "Este CPF já está em uso.";
                return RedirectToAction("GerenciarEquipe");
            }

            var associado = new Usuario
            {
                Nome            = nome.Trim(),
                Email           = email.ToLower().Trim(),
                Telefone        = telLimpo,
                CPF             = cpfLimpo,
                Senha           = BCrypt.Net.BCrypt.HashPassword(senha),
                TipoUsuario     = TipoUsuario.Associado,
                UsuarioMasterId = masterId,
                Status          = StatusUsuario.Ativo,
                DataCriacao     = DateTime.Now
            };

            _context.Usuarios.Add(associado);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Agente {nome.Trim()} criado com sucesso!";
            return RedirectToAction("GerenciarEquipe");
        }

        // POST /Usuario/EditarAssociado
        // Edita dados principais de um associado da própria equipe.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarAssociado(
            Guid id, string nome, string email, string telefone, StatusUsuario status)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            var associado = await ObterAssociadoDoMaster(id);
            if (associado == null)
            {
                TempData["Erro"] = "Membro não encontrado ou sem permissão.";
                return RedirectToAction("GerenciarEquipe");
            }

            var telLimpo = Regex.Replace(telefone ?? "", @"[^\d]", "");

            // Unicidade de e-mail (exceto o próprio registro)
            if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == email.ToLower() && u.Id != id))
            {
                TempData["Erro"] = "Este e-mail já está em uso por outra conta.";
                return RedirectToAction("GerenciarEquipe");
            }

            // Unicidade de telefone (exceto o próprio registro)
            if (!string.IsNullOrEmpty(telLimpo) &&
                await _context.Usuarios.AnyAsync(u => u.Telefone == telLimpo && u.Id != id))
            {
                TempData["Erro"] = "Este telefone já está em uso por outra conta.";
                return RedirectToAction("GerenciarEquipe");
            }

            associado.Nome     = nome.Trim();
            associado.Email    = email.ToLower().Trim();
            associado.Telefone = telLimpo;
            associado.Status   = status;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Dados de {associado.Nome} atualizados com sucesso.";
            return RedirectToAction("GerenciarEquipe");
        }

        // POST /Usuario/AlterarSenhaAssociado
        // Apenas o master pode alterar a senha de seus associados.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSenhaAssociado(Guid id, string novaSenha, string confirmarSenha)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6)
            {
                TempData["Erro"] = "A nova senha deve ter pelo menos 6 caracteres.";
                return RedirectToAction("GerenciarEquipe");
            }

            if (novaSenha != confirmarSenha)
            {
                TempData["Erro"] = "As senhas não conferem.";
                return RedirectToAction("GerenciarEquipe");
            }

            var associado = await ObterAssociadoDoMaster(id);
            if (associado == null)
            {
                TempData["Erro"] = "Membro não encontrado ou sem permissão.";
                return RedirectToAction("GerenciarEquipe");
            }

            associado.Senha = BCrypt.Net.BCrypt.HashPassword(novaSenha);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Senha de {associado.Nome} alterada com sucesso.";
            return RedirectToAction("GerenciarEquipe");
        }

        // POST /Usuario/InativarAssociado
        // Atalho semântico para inativação — preserva histórico e propostas.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InativarAssociado(Guid id)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            var associado = await ObterAssociadoDoMaster(id);
            if (associado == null)
            {
                TempData["Erro"] = "Membro não encontrado ou sem permissão.";
                return RedirectToAction("GerenciarEquipe");
            }

            associado.Status = StatusUsuario.Inativo;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{associado.Nome} foi inativado. Propostas existentes foram preservadas.";
            return RedirectToAction("GerenciarEquipe");
        }

        // POST /Usuario/AlterarStatusAssociado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarStatusAssociado(Guid id, StatusUsuario status)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!IsMaster()) return RedirectToAction("Index", "Proposta");

            var associado = await ObterAssociadoDoMaster(id);
            if (associado == null) return NotFound();

            associado.Status = status;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Status atualizado.";
            return RedirectToAction("GerenciarEquipe");
        }
    }
}