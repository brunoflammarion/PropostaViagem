using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class PlatformAdminController : Controller
    {
        private readonly ApplicationDbContext  _db;
        private readonly PlatformMetricsService _metrics;
        private readonly DemonstracaoService    _demo;

        public PlatformAdminController(ApplicationDbContext db, PlatformMetricsService metrics, DemonstracaoService demo)
        {
            _db      = db;
            _metrics = metrics;
            _demo    = demo;
        }

        private bool AdminLogado() =>
            HttpContext.Session.GetString("IsPlatformAdmin") == "true";

        // ── Auth ──────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            if (AdminLogado()) return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string senha)
        {
            var admin = await _db.AdminsPlataforma
                .FirstOrDefaultAsync(a => a.Email == email && a.Ativo);

            if (admin == null || !BCrypt.Net.BCrypt.Verify(senha, admin.Senha))
            {
                ViewBag.Erro = "Email ou senha incorretos.";
                return View();
            }

            HttpContext.Session.SetString("IsPlatformAdmin", "true");
            HttpContext.Session.SetString("PlatformAdminId", admin.Id.ToString());
            HttpContext.Session.SetString("PlatformAdminNome", admin.Nome);

            return RedirectToAction("Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("IsPlatformAdmin");
            HttpContext.Session.Remove("PlatformAdminId");
            HttpContext.Session.Remove("PlatformAdminNome");
            return RedirectToAction("Login");
        }

        // ── Dashboard ─────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Dashboard";
            return View(await _metrics.GetDashboardMetrics());
        }

        // ── Agências ──────────────────────────────────────────────────────────

        public async Task<IActionResult> Agencias(string? status = null)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Agências";
            return View(await _metrics.GetAgenciasList(status));
        }

        public async Task<IActionResult> Agencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            var vm = await _metrics.GetAgenciaDetail(id);
            if (vm == null) return NotFound();
            ViewData["Title"] = vm.NomeAgencia;
            return View(vm);
        }

        // ── Ações ─────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearAgencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var master = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == TipoUsuario.Master);

            if (master == null) return NotFound();

            master.Status = StatusUsuario.Bloqueado;

            await _db.Usuarios
                .Where(u => u.UsuarioMasterId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, StatusUsuario.Bloqueado));

            await _db.SaveChangesAsync();

            TempData["Mensagem"] = $"Agência \"{master.NomeAgencia ?? master.Nome}\" bloqueada. Todos os logins foram bloqueados.";
            TempData["MensagemTipo"] = "danger";

            return RedirectToAction("Agencia", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesbloquearAgencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var master = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == TipoUsuario.Master);

            if (master == null) return NotFound();

            master.Status = StatusUsuario.Ativo;

            await _db.Usuarios
                .Where(u => u.UsuarioMasterId == id && u.Status == StatusUsuario.Bloqueado)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, StatusUsuario.Ativo));

            await _db.SaveChangesAsync();

            TempData["Mensagem"] = $"Agência \"{master.NomeAgencia ?? master.Nome}\" reativada com sucesso.";
            TempData["MensagemTipo"] = "success";

            return RedirectToAction("Agencia", new { id });
        }

        // ── Reaplicar demonstração para agência específica ────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReaplicarDemonstracao(Guid masterId)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            try
            {
                await _demo.ReaplicarPendentesAsync(masterId);
                TempData["Mensagem"]     = "Demonstração reaplicada com sucesso.";
                TempData["MensagemTipo"] = "success";
            }
            catch (Exception ex)
            {
                TempData["Mensagem"]     = $"Erro ao reaplicar: {ex.Message}";
                TempData["MensagemTipo"] = "danger";
            }

            return RedirectToAction("Agencia", new { id = masterId });
        }

        // ── Conteúdos de Demonstração ─────────────────────────────────────────

        public async Task<IActionResult> Demonstracao()
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Conteúdos de Demonstração";

            var modelos = await _db.ConteudosDemonstracao
                .Include(c => c.CriadoPorAdmin)
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            // Enriquecer com dados das entidades de origem (nome + tipo)
            var vm = new List<ConteudoDemonstracaoItemVm>();
            foreach (var m in modelos)
            {
                string nomeEntidade = "(não encontrada)";
                string? fotoCapa    = null;

                if (m.TipoConteudo == TipoConteudoDemonstracao.Proposta)
                {
                    var p = await _db.Propostas.AsNoTracking()
                        .Where(x => x.Id == m.EntidadeOrigemId)
                        .Select(x => new { x.Titulo, x.FotoCapa })
                        .FirstOrDefaultAsync();
                    if (p != null) { nomeEntidade = p.Titulo; fotoCapa = p.FotoCapa; }
                }
                else
                {
                    var o = await _db.Ofertas.AsNoTracking()
                        .Where(x => x.Id == m.EntidadeOrigemId)
                        .Select(x => new { x.Nome, x.ImagemPrincipalPath })
                        .FirstOrDefaultAsync();
                    if (o != null) { nomeEntidade = o.Nome; fotoCapa = o.ImagemPrincipalPath; }
                }

                var totalAplicados = await _db.ConteudosDemonstracaoAplicados
                    .CountAsync(a => a.ConteudoDemonstracaoId == m.Id
                               && a.StatusAplicacao == StatusAplicacaoDemonstracao.Sucesso);

                vm.Add(new ConteudoDemonstracaoItemVm
                {
                    Modelo        = m,
                    NomeEntidade  = nomeEntidade,
                    FotoCapa      = fotoCapa,
                    TotalAplicados= totalAplicados,
                });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarConteudo(TipoConteudoDemonstracao tipo, Guid entidadeId, string? nomeAdmin)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var adminId = Guid.Parse(HttpContext.Session.GetString("PlatformAdminId")!);

            // Validar que a entidade existe
            bool existe = tipo == TipoConteudoDemonstracao.Proposta
                ? await _db.Propostas.AnyAsync(p => p.Id == entidadeId)
                : await _db.Ofertas.AnyAsync(o => o.Id == entidadeId);

            if (!existe)
            {
                TempData["Mensagem"]     = "Entidade não encontrada.";
                TempData["MensagemTipo"] = "danger";
                return RedirectToAction("Demonstracao");
            }

            // Evitar duplicata
            var jaExiste = await _db.ConteudosDemonstracao
                .AnyAsync(c => c.EntidadeOrigemId == entidadeId && c.TipoConteudo == tipo);
            if (jaExiste)
            {
                TempData["Mensagem"]     = "Este conteúdo já está na lista de demonstração.";
                TempData["MensagemTipo"] = "warning";
                return RedirectToAction("Demonstracao");
            }

            var maxOrdem = await _db.ConteudosDemonstracao.MaxAsync(c => (int?)c.Ordem) ?? 0;

            _db.ConteudosDemonstracao.Add(new ConteudoDemonstracao
            {
                TipoConteudo          = tipo,
                EntidadeOrigemId      = entidadeId,
                NomeAdministrativo    = string.IsNullOrWhiteSpace(nomeAdmin) ? null : nomeAdmin.Trim(),
                Ativo                 = true,
                AplicarAutomaticamente= true,
                Ordem                 = maxOrdem + 1,
                DataCriacao           = DateTime.Now,
                CriadoPorAdminId      = adminId,
            });
            await _db.SaveChangesAsync();

            TempData["Mensagem"]     = "Conteúdo adicionado ao catálogo de demonstração.";
            TempData["MensagemTipo"] = "success";
            return RedirectToAction("Demonstracao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleConteudo(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var c = await _db.ConteudosDemonstracao.FindAsync(id);
            if (c == null) return NotFound();

            c.Ativo           = !c.Ativo;
            c.DataAtualizacao = DateTime.Now;
            c.AtualizadoPorAdminId = Guid.Parse(HttpContext.Session.GetString("PlatformAdminId")!);
            await _db.SaveChangesAsync();

            return RedirectToAction("Demonstracao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverConteudo(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var c = await _db.ConteudosDemonstracao.FindAsync(id);
            if (c == null) return NotFound();

            _db.ConteudosDemonstracao.Remove(c);
            await _db.SaveChangesAsync();

            TempData["Mensagem"]     = "Conteúdo removido do catálogo.";
            TempData["MensagemTipo"] = "success";
            return RedirectToAction("Demonstracao");
        }

        // AJAX: preview de entidade antes de adicionar
        [HttpGet]
        public async Task<IActionResult> BuscarEntidade(string tipo, Guid id)
        {
            if (!AdminLogado()) return Unauthorized();

            if (tipo == "Proposta")
            {
                var p = await _db.Propostas.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new { x.Id, x.Titulo, x.FotoCapa, x.StatusProposta, x.DataCriacao,
                                       DestinCount = x.Destinos.Count })
                    .FirstOrDefaultAsync();
                if (p == null) return Json(new { encontrado = false });
                return Json(new { encontrado = true, tipo = "Proposta",
                    nome  = p.Titulo,
                    foto  = p.FotoCapa,
                    extra = $"{p.DestinCount} destino(s) · {p.StatusProposta}",
                    data  = p.DataCriacao.ToString("dd/MM/yyyy") });
            }
            else
            {
                var o = await _db.Ofertas.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new { x.Id, x.Nome, x.ImagemPrincipalPath, x.TemplateId, x.DataCriacao })
                    .FirstOrDefaultAsync();
                if (o == null) return Json(new { encontrado = false });
                return Json(new { encontrado = true, tipo = "Oferta",
                    nome  = o.Nome,
                    foto  = o.ImagemPrincipalPath,
                    extra = $"Template: {o.TemplateId}",
                    data  = o.DataCriacao.ToString("dd/MM/yyyy") });
            }
        }
    }

    // ViewModel auxiliar para a tela de demonstração
    public class ConteudoDemonstracaoItemVm
    {
        public ConteudoDemonstracao Modelo        { get; set; } = null!;
        public string               NomeEntidade  { get; set; } = "";
        public string?              FotoCapa      { get; set; }
        public int                  TotalAplicados{ get; set; }
    }
}
