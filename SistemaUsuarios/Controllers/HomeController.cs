using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Infrastructure;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITarefaService _tarefaService;
        private readonly IAppClock _clock;

        public HomeController(ApplicationDbContext context, ITarefaService tarefaService, IAppClock clock)
        {
            _context       = context;
            _tarefaService = tarefaService;
            _clock         = clock;
        }

        public async Task<IActionResult> Index()
        {
            var idStr = HttpContext.Session.GetString("UsuarioId");
            if (idStr == null)
                return RedirectToAction("Login", "Auth");

            var usuarioId = Guid.Parse(idStr);
            var isMaster  = HttpContext.Session.GetString("TipoUsuario") != "Associado";
            var masterId  = isMaster ? usuarioId
                : Guid.TryParse(HttpContext.Session.GetString("UsuarioMasterId"), out var mid) ? mid : usuarioId;

            var hoje = _clock.Today;
            var agora = _clock.UtcNow;

            // Propostas do escopo do usuário
            var propostas = await _context.Propostas
                .Where(p => isMaster
                    ? p.UsuarioMasterId == masterId
                    : p.UsuarioResponsavelId == usuarioId)
                .Include(p => p.Cliente)
                .Include(p => p.Destinos)
                .AsNoTracking()
                .ToListAsync();

            var propostaIds = propostas.Select(p => p.Id).ToList();

            // Visualizações dos últimos 7 dias
            var viz7Cutoff = hoje.AddDays(-7);
            var visualizacoes7d = propostaIds.Any()
                ? await _context.PropostaVisualizacoes
                    .Where(v => propostaIds.Contains(v.PropostaId) && v.DataCriacao >= viz7Cutoff)
                    .Select(v => new Viz7 { PropostaId = v.PropostaId, DataCriacao = v.DataCriacao })
                    .AsNoTracking()
                    .ToListAsync()
                : new List<Viz7>();

            // Leads da agência
            var leads = await _context.Leads
                .Where(l => l.UsuarioId == masterId)
                .AsNoTracking()
                .ToListAsync();

            // Captação
            var captacaoSettings = await _context.LeadCaptureSettings
                .Where(s => s.UsuarioId == masterId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            var masterInfo = await _context.Usuarios
                .Where(u => u.Id == masterId)
                .Select(u => new { u.NomeAgencia, u.SlugAgencia })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            // Cards
            var propostasAbertas = propostas
                .Where(p => p.StatusProposta != StatusProposta.Aprovada &&
                            p.StatusProposta != StatusProposta.Cancelada)
                .Select(p => p.Id).ToHashSet();

            var vizIds = visualizacoes7d.Select(v => v.PropostaId).Distinct().ToHashSet();

            var vm = new HomeDashboardViewModel
            {
                NomeUsuario              = HttpContext.Session.GetString("UsuarioNome") ?? "Agente",
                FotoPath                 = HttpContext.Session.GetString("FotoPath"),
                LeadsNovos               = leads.Count(l => l.Status == LeadStatus.Novo),
                PropostasVisualizadas7d  = vizIds.Intersect(propostasAbertas).Count(),
                ViagensAndamento         = propostas.Count(p =>
                    p.StatusProposta == StatusProposta.Aprovada &&
                    p.DataInicio.HasValue && p.DataFim.HasValue &&
                    p.DataInicio.Value.Date <= hoje && p.DataFim.Value.Date >= hoje),
                ViagensProximas15d       = propostas.Count(p =>
                    p.StatusProposta == StatusProposta.Aprovada &&
                    p.DataInicio.HasValue &&
                    p.DataInicio.Value.Date > hoje &&
                    p.DataInicio.Value.Date <= hoje.AddDays(15))
            };

            // Fila de tarefas — mesma fonte que /Tarefa, max 5 por aba
            var todasAll      = await _tarefaService.ListarTodasPendentesAsync(usuarioId);
            var atrasadasAll  = await _tarefaService.ListarAtrasadasAsync(usuarioId);
            var hojeAll       = await _tarefaService.ListarHojeAsync(usuarioId);
            var semanaAll     = await _tarefaService.ListarPorUsuarioAsync(usuarioId,
                de: hoje.AddDays(1), ate: hoje.AddDays(7), status: TarefaStatus.Pendente);
            var concluidasAll = await _tarefaService.ListarPorUsuarioAsync(usuarioId,
                de: hoje.AddDays(-30), status: TarefaStatus.Concluida);

            vm.TotalTodas        = todasAll.Count;
            vm.TotalAtrasadas    = atrasadasAll.Count;
            vm.TotalHoje         = hojeAll.Count;
            vm.TotalSemana       = semanaAll.Count;
            vm.TarefasTodas      = todasAll.Take(5).ToList();
            vm.TarefasAtrasadas  = atrasadasAll.Take(5).ToList();
            vm.TarefasHoje       = hojeAll.Take(5).ToList();
            vm.TarefasSemana     = semanaAll.Take(5).ToList();
            vm.TarefasConcluidas = concluidasAll.Take(5).ToList();

            BuildContinueList(vm, propostas, leads, agora);
            BuildViagensAgenda(vm, propostas, hoje);

            var publicUrl = captacaoSettings != null && !string.IsNullOrEmpty(masterInfo?.SlugAgencia)
                ? $"{Request.Scheme}://{Request.Host}/{masterInfo.SlugAgencia}"
                : null;

            vm.Captacao = new CaptacaoHomeInfo
            {
                IsActive    = captacaoSettings?.IsActive ?? false,
                NomeAgencia = masterInfo?.NomeAgencia,
                SlugAgencia = masterInfo?.SlugAgencia,
                PublicUrl   = publicUrl
            };

            return View(vm);
        }

        // ── Continue de onde parou ────────────────────────────────────────────────

        private static void BuildContinueList(HomeDashboardViewModel vm, List<Proposta> propostas,
            List<Lead> leads, DateTime agora)
        {
            var items = new List<ContinueItem>();

            foreach (var p in propostas
                .Where(p => p.StatusProposta != StatusProposta.Cancelada)
                .OrderByDescending(p => p.DataModificacao ?? p.DataCriacao)
                .Take(4))
            {
                var (lbl, css) = p.StatusProposta switch
                {
                    StatusProposta.Rascunho  => ("Rascunho",  "cnt-rascunho"),
                    StatusProposta.Enviada   => ("Enviada",   "cnt-enviada"),
                    StatusProposta.Aprovada  => ("Aprovada",  "cnt-aprovada"),
                    StatusProposta.Rejeitada => ("Rejeitada", "cnt-rejeitada"),
                    _                        => ("—",         "")
                };
                items.Add(new ContinueItem
                {
                    Tipo = "Proposta", TipoIcon = "fas fa-file-alt",
                    Nome              = p.Titulo,
                    Destino           = p.Destinos.FirstOrDefault()?.Nome,
                    Status            = lbl, StatusCss = css,
                    UltimaAtualizacao = p.DataModificacao ?? p.DataCriacao,
                    Url               = $"/Proposta/Editar/{p.Id}",
                    AcaoLabel         = "Continuar"
                });
            }

            foreach (var l in leads
                .Where(l => l.Status != LeadStatus.Perdido && l.Status != LeadStatus.Convertido)
                .OrderByDescending(l => l.CreatedAt)
                .Take(3))
            {
                var lbl = l.Status switch
                {
                    LeadStatus.Novo         => "Novo",
                    LeadStatus.Contatado    => "Contatado",
                    LeadStatus.EmNegociacao => "Em negociação",
                    _                       => "—"
                };
                items.Add(new ContinueItem
                {
                    Tipo = "Lead", TipoIcon = "fas fa-user-plus",
                    Nome              = l.FullName,
                    Destino           = l.Destination,
                    Status            = lbl,
                    StatusCss         = l.Status == LeadStatus.Novo ? "cnt-lead-novo" : "cnt-lead",
                    UltimaAtualizacao = l.CreatedAt,
                    Url               = "/Lead",
                    AcaoLabel         = "Ver"
                });
            }

            vm.ContinueList = items
                .OrderByDescending(i => i.UltimaAtualizacao)
                .Take(6)
                .ToList();
        }

        // ── Viagens e agenda ─────────────────────────────────────────────────────

        private static void BuildViagensAgenda(HomeDashboardViewModel vm, List<Proposta> propostas, DateTime hoje)
        {
            var items = new List<TravelAgendaItem>();

            foreach (var p in propostas.Where(p =>
                p.StatusProposta == StatusProposta.Aprovada &&
                p.DataInicio.HasValue && p.DataFim.HasValue &&
                p.DataInicio.Value.Date <= hoje && p.DataFim.Value.Date >= hoje))
            {
                items.Add(new TravelAgendaItem
                {
                    PropostaTitulo = p.Titulo, ClienteNome = p.Cliente?.Nome,
                    Destino        = p.Destinos.FirstOrDefault()?.Nome,
                    DataInicio     = p.DataInicio!.Value, DataFim = p.DataFim!.Value,
                    EmAndamento    = true, Url = $"/Proposta/Editar/{p.Id}"
                });
            }

            foreach (var p in propostas.Where(p =>
                p.StatusProposta == StatusProposta.Aprovada &&
                p.DataInicio.HasValue && p.DataFim.HasValue &&
                p.DataInicio.Value.Date > hoje &&
                p.DataInicio.Value.Date <= hoje.AddDays(15)))
            {
                items.Add(new TravelAgendaItem
                {
                    PropostaTitulo = p.Titulo, ClienteNome = p.Cliente?.Nome,
                    Destino        = p.Destinos.FirstOrDefault()?.Nome,
                    DataInicio     = p.DataInicio!.Value, DataFim = p.DataFim!.Value,
                    EmAndamento    = false, Url = $"/Proposta/Editar/{p.Id}"
                });
            }

            vm.ViagensAgenda = items.OrderBy(i => i.DataInicio).ToList();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            return Content("Ocorreu um erro interno. Tente novamente.", "text/plain");
        }

        private class Viz7
        {
            public Guid PropostaId { get; set; }
            public DateTime DataCriacao { get; set; }
        }
    }
}
