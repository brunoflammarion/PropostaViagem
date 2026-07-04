using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class TarefaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITarefaService _tarefaService;

        public TarefaController(ApplicationDbContext context, ITarefaService tarefaService)
        {
            _context      = context;
            _tarefaService = tarefaService;
        }

        private bool UsuarioLogado() => HttpContext.Session.GetString("UsuarioId") != null;
        private Guid UsuarioId()     => Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        // ── Index ────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var uid  = UsuarioId();
            var hoje = DateTime.Today;

            var vm = new TarefaIndexViewModel
            {
                TarefasHoje       = await _tarefaService.ListarHojeAsync(uid),
                TarefasAtrasadas  = await _tarefaService.ListarAtrasadasAsync(uid),
                TarefasSemana     = await _tarefaService.ListarPorUsuarioAsync(uid,
                    de: hoje.AddDays(1), ate: hoje.AddDays(7), status: TarefaStatus.Pendente),
                TarefasConcluidas = await _tarefaService.ListarPorUsuarioAsync(uid,
                    de: hoje.AddDays(-30), status: TarefaStatus.Concluida),
                Clientes  = await ClientesSelectList(uid),
                Propostas = await PropostasSelectList(uid)
            };

            return View(vm);
        }

        // ── CRUD ─────────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Criar([FromForm] CriarTarefaForm form)
        {
            if (!UsuarioLogado()) return Json(new { ok = false });

            if (string.IsNullOrWhiteSpace(form.Titulo))
                return Json(new { ok = false, erro = "Título obrigatório." });

            await _tarefaService.CriarTarefaManualAsync(new CriarTarefaDto
            {
                UsuarioId      = UsuarioId(),
                ClienteId      = form.ClienteId,
                PropostaId     = form.PropostaId,
                Titulo         = form.Titulo,
                Descricao      = form.Descricao,
                DataVencimento = form.DataVencimento,
                Tipo           = form.Tipo,
                Prioridade     = form.Prioridade
            });

            return Json(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> Concluir(Guid id)
        {
            if (!UsuarioLogado()) return Json(new { ok = false });
            await _tarefaService.ConcluirTarefaAsync(id, UsuarioId());
            return Json(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> Cancelar(Guid id)
        {
            if (!UsuarioLogado()) return Json(new { ok = false });
            await _tarefaService.CancelarTarefaAsync(id, UsuarioId());
            return Json(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> Editar([FromForm] EditarTarefaForm form)
        {
            if (!UsuarioLogado()) return Json(new { ok = false });

            await _tarefaService.EditarTarefaAsync(new EditarTarefaDto
            {
                Id             = form.Id,
                UsuarioId      = UsuarioId(),
                Titulo         = form.Titulo,
                Descricao      = form.Descricao,
                DataVencimento = form.DataVencimento,
                Tipo           = form.Tipo,
                Prioridade     = form.Prioridade
            });

            return Json(new { ok = true });
        }

        // ── Configurações de lembretes ────────────────────────────────────────────

        public async Task<IActionResult> Configuracoes()
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var uid     = UsuarioId();
            var configs = await _tarefaService.ObterConfiguracoesAsync(uid);

            var vm = configs.Select(c => new ConfiguracaoLembreteViewModel
            {
                Id               = c.Id,
                TemplateCodigo   = c.TemplateCodigo,
                Titulo           = TarefaService.TituloTemplate(c.TemplateCodigo),
                Descricao        = TarefaService.DescricaoTemplate(c.TemplateCodigo),
                Tipo             = c.Tipo,
                Habilitado       = c.Habilitado,
                OffsetDias       = c.OffsetDias,
                MomentoReferencia = c.MomentoReferencia,
                TextoQuando      = TarefaService.TextoQuandoTemplate(c.TemplateCodigo, c.OffsetDias, c.MomentoReferencia)
            }).ToList();

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SalvarConfiguracoes([FromForm] SalvarConfigsForm form)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var uid = UsuarioId();
            var configs = (form.Codigos ?? new List<string>())
                .Select(c => (c, form.Habilitados?.Contains(c) ?? false))
                .ToList();

            await _tarefaService.SalvarConfiguracoesAsync(uid, configs);

            TempData["Sucesso"] = "Configurações de lembretes salvas!";
            return RedirectToAction(nameof(Configuracoes));
        }

        // ── Gatilho manual (scheduler) ───────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ProcessarLembretes()
        {
            if (!UsuarioLogado()) return Json(new { ok = false });
            await _tarefaService.ProcessarLembretesDoUsuarioAsync(UsuarioId());
            return Json(new { ok = true, msg = "Lembretes processados com sucesso." });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<List<SelectListItem>> ClientesSelectList(Guid uid)
        {
            var lista = await _context.Clientes
                .Where(c => c.UsuarioId == uid && !c.IsDeleted)
                .OrderBy(c => c.Nome)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nome })
                .AsNoTracking()
                .ToListAsync();

            lista.Insert(0, new SelectListItem { Value = "", Text = "— Nenhum —" });
            return lista;
        }

        private async Task<List<SelectListItem>> PropostasSelectList(Guid uid)
        {
            var lista = await _context.Propostas
                .Where(p => (p.UsuarioResponsavelId == uid || p.UsuarioMasterId == uid)
                    && p.StatusProposta != StatusProposta.Cancelada)
                .OrderByDescending(p => p.DataCriacao)
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Titulo })
                .AsNoTracking()
                .ToListAsync();

            lista.Insert(0, new SelectListItem { Value = "", Text = "— Nenhuma —" });
            return lista;
        }
    }

    // ── Form models ──────────────────────────────────────────────────────────────
    public class CriarTarefaForm
    {
        public string   Titulo         { get; set; } = "";
        public string?  Descricao      { get; set; }
        public DateTime DataVencimento { get; set; }
        public string?  Tipo           { get; set; }
        public string?  Prioridade     { get; set; }
        public Guid?    ClienteId      { get; set; }
        public Guid?    PropostaId     { get; set; }
    }

    public class EditarTarefaForm
    {
        public Guid     Id             { get; set; }
        public string   Titulo         { get; set; } = "";
        public string?  Descricao      { get; set; }
        public DateTime DataVencimento { get; set; }
        public string?  Tipo           { get; set; }
        public string?  Prioridade     { get; set; }
    }

    public class SalvarConfigsForm
    {
        public List<string>? Codigos     { get; set; }
        public List<string>? Habilitados { get; set; }
    }
}
