using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;

namespace SistemaUsuarios.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
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

            var hoje = DateTime.Today;
            var agora = DateTime.Now;

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

            BuildFilaAtencao(vm, propostas, leads, visualizacoes7d, hoje, agora);
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

        // ── Fila de atenção ──────────────────────────────────────────────────────

        private static void BuildFilaAtencao(HomeDashboardViewModel vm, List<Proposta> propostas,
            List<Lead> leads, List<Viz7> viz7d, DateTime hoje, DateTime agora)
        {
            var ultimaViz = viz7d
                .GroupBy(v => v.PropostaId)
                .ToDictionary(g => g.Key, g => g.Max(v => v.DataCriacao));

            var items = new List<AttentionItem>();

            // Prioridade 1 — Viagens em andamento
            foreach (var p in propostas.Where(p =>
                p.StatusProposta == StatusProposta.Aprovada &&
                p.DataInicio.HasValue && p.DataFim.HasValue &&
                p.DataInicio.Value.Date <= hoje && p.DataFim.Value.Date >= hoje))
            {
                var diasRet = (int)(p.DataFim!.Value.Date - hoje).TotalDays;
                var destino = p.Destinos.FirstOrDefault()?.Nome ?? p.Titulo;
                items.Add(new AttentionItem
                {
                    TipoIcon = "fas fa-plane", TipoBadge = "Em viagem", TipoCss = "atq-viagem",
                    NomeContato      = p.Cliente?.Nome ?? p.Titulo,
                    Descricao        = $"em viagem — {destino}",
                    Contexto         = diasRet == 0 ? "Retorna hoje" : $"Retorna em {diasRet}d",
                    Url              = $"/Proposta/Editar/{p.Id}",
                    AcaoLabel        = "Ver viagem",
                    Prioridade       = 1,
                    SecondarySortKey = p.DataFim.Value
                });
            }

            // Prioridade 2 — Leads novos
            foreach (var l in leads.Where(l => l.Status == LeadStatus.Novo))
            {
                var diff  = agora - l.CreatedAt;
                var tempo = diff.TotalHours < 1  ? $"há {(int)diff.TotalMinutes}min"
                          : diff.TotalHours < 24 ? $"há {(int)diff.TotalHours}h"
                          : $"há {(int)diff.TotalDays}d";
                items.Add(new AttentionItem
                {
                    TipoIcon = "fas fa-user-plus", TipoBadge = "Lead novo", TipoCss = "atq-lead",
                    NomeContato      = l.FullName,
                    Descricao        = $"quer ir para {l.Destination}",
                    Contexto         = $"Recebido {tempo}",
                    Url              = "/Lead",
                    AcaoLabel        = "Responder",
                    Prioridade       = 2,
                    SecondarySortKey = l.CreatedAt
                });
            }

            // Prioridade 3 — Propostas visualizadas (não fechadas)
            var abertas = propostas.Where(p =>
                p.StatusProposta != StatusProposta.Aprovada &&
                p.StatusProposta != StatusProposta.Cancelada &&
                p.StatusProposta != StatusProposta.Rejeitada).ToList();

            foreach (var p in abertas)
            {
                if (!ultimaViz.TryGetValue(p.Id, out var ultima)) continue;
                var diff  = agora - ultima;
                var tempo = diff.TotalHours < 1  ? $"há {(int)diff.TotalMinutes}min"
                          : diff.TotalHours < 24 ? $"hoje às {ultima:HH:mm}"
                          : $"há {(int)diff.TotalDays}d";
                items.Add(new AttentionItem
                {
                    TipoIcon = "fas fa-eye", TipoBadge = "Visualizada", TipoCss = "atq-viz",
                    NomeContato      = p.Cliente?.Nome ?? p.Titulo,
                    Descricao        = $"abriu a proposta \"{p.Titulo}\"",
                    Contexto         = $"Viu {tempo}",
                    Url              = $"/Proposta/Editar/{p.Id}",
                    AcaoLabel        = "Follow-up",
                    Prioridade       = 3,
                    SecondarySortKey = ultima
                });
            }

            // Prioridade 4 — Viagens próximas 15d
            foreach (var p in propostas.Where(p =>
                p.StatusProposta == StatusProposta.Aprovada &&
                p.DataInicio.HasValue && p.DataFim.HasValue &&
                p.DataInicio.Value.Date > hoje &&
                p.DataInicio.Value.Date <= hoje.AddDays(15)))
            {
                var dias    = (int)(p.DataInicio!.Value.Date - hoje).TotalDays;
                var destino = p.Destinos.FirstOrDefault()?.Nome ?? p.Titulo;
                items.Add(new AttentionItem
                {
                    TipoIcon = "fas fa-plane-departure", TipoBadge = "Próxima viagem", TipoCss = "atq-proxima",
                    NomeContato      = p.Cliente?.Nome ?? p.Titulo,
                    Descricao        = $"viagem para {destino}",
                    Contexto         = dias == 1 ? "Embarca amanhã!" : $"Embarca em {dias} dias",
                    Url              = $"/Proposta/Editar/{p.Id}",
                    AcaoLabel        = "Revisar",
                    Prioridade       = 4,
                    SecondarySortKey = p.DataInicio.Value
                });
            }

            // Prioridade 5 — Propostas paradas há 3d+ (sem visualização recente)
            foreach (var p in abertas.Where(p =>
                p.StatusProposta == StatusProposta.Rascunho ||
                p.StatusProposta == StatusProposta.Enviada))
            {
                if (ultimaViz.ContainsKey(p.Id)) continue;
                var dataRef = p.DataModificacao ?? p.DataCriacao;
                var dias    = (int)(agora - dataRef).TotalDays;
                if (dias < 3) continue;
                var statusLabel = p.StatusProposta == StatusProposta.Rascunho ? "Rascunho" : "Enviada";
                items.Add(new AttentionItem
                {
                    TipoIcon = "fas fa-clock", TipoBadge = "Follow-up", TipoCss = "atq-followup",
                    NomeContato      = p.Cliente?.Nome ?? p.Titulo,
                    Descricao        = $"sem atualização há {dias} dias",
                    Contexto         = statusLabel,
                    Url              = $"/Proposta/Editar/{p.Id}",
                    AcaoLabel        = "Abrir",
                    Prioridade       = 5,
                    SecondarySortKey = dataRef
                });
            }

            vm.FilaAtencao = items.Where(i => i.Prioridade == 1).OrderBy(i => i.SecondarySortKey)
                .Concat(items.Where(i => i.Prioridade == 2).OrderByDescending(i => i.SecondarySortKey))
                .Concat(items.Where(i => i.Prioridade == 3).OrderByDescending(i => i.SecondarySortKey))
                .Concat(items.Where(i => i.Prioridade == 4).OrderBy(i => i.SecondarySortKey))
                .Concat(items.Where(i => i.Prioridade == 5).OrderBy(i => i.SecondarySortKey))
                .Take(8)
                .ToList();
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

        private class Viz7
        {
            public Guid PropostaId { get; set; }
            public DateTime DataCriacao { get; set; }
        }
    }
}
