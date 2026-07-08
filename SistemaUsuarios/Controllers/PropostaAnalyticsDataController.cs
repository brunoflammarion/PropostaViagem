using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels.Analytics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace SistemaUsuarios.Controllers
{
    public class PropostaAnalyticsDataController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropostaAnalyticsDataController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado()
        {
            return HttpContext.Session.GetString("UsuarioId") != null;
        }

        private Guid ObterUsuarioLogadoId()
        {
            var usuarioIdString = HttpContext.Session.GetString("UsuarioId");
            return Guid.Parse(usuarioIdString);
        }

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        // DASHBOARD GERAL - Nível 1
        public async Task<IActionResult> Dashboard()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var totalPropostas = await _context.Propostas.CountAsync(p =>
                isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId);

            var totalVisualizacoes = await _context.PropostaVisualizacoes
                .Where(v => isMaster
                    ? v.Proposta.UsuarioMasterId == usuarioId
                    : v.Proposta.UsuarioResponsavelId == usuarioId)
                .CountAsync();

            var propostasComVisualizacao = await _context.Propostas
                .CountAsync(p =>
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                    && p.PropostaVisualizacoes.Any());

            var seteAtras = DateTime.Now.AddDays(-7);
            var clientesQuentes = await _context.Propostas
                .CountAsync(p =>
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                    && p.PropostaVisualizacoes.Any(v => v.DataHoraInicio >= seteAtras)
                    && p.StatusProposta != StatusProposta.Aprovada
                    && p.StatusProposta != StatusProposta.Cancelada);

            var viewModel = new DashboardAnalyticsViewModel
            {
                UsuarioId = usuarioId,
                TotalPropostas = totalPropostas,
                TotalVisualizacoes = totalVisualizacoes,
                PropostasComVisualizacao = propostasComVisualizacao,
                ClientesQuentes = clientesQuentes,
                TaxaPropostasVisualizadas = totalPropostas > 0
                    ? Math.Round((double)propostasComVisualizacao / totalPropostas * 100, 0)
                    : 0,
                VisualizacoesUltimos30Dias = await ObterVisualizacoesUltimos30Dias(usuarioId, isMaster),
                PropostasPopulares = await ObterPropostasPopulares(usuarioId, isMaster),
                PropostasRadar = await ObterPropostasRadar(usuarioId, isMaster),
                AtividadeRecente = await ObterAtividadeRecente(usuarioId, isMaster),
                OportunidadesFollowUp = await ObterOportunidadesFollowUp(usuarioId, isMaster),
                EstatisticasGerais = await ObterEstatisticasGerais(usuarioId, isMaster),
                LocalizacoesAcessos = await ObterLocalizacoesAcessos(usuarioId, isMaster),
                DispositivosAcessos = await ObterDispositivosAcessos(usuarioId, isMaster)
            };

            return View(viewModel);
        }

        // DASHBOARD ESPECÍFICO - Nível 2 - CORRIGIDO
        [HttpGet("PropostaAnalyticsData/PropostaDetalhada/{propostaId}")]
        public async Task<IActionResult> PropostaDetalhada(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            // Verificar se a proposta pertence ao usuário (respeitando hierarquia)
            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada ou você não tem permissão para visualizá-la.";
                return RedirectToAction("Dashboard");
            }

            var viewModel = new PropostaAnalyticsDetalhadaViewModel
            {
                Proposta = proposta,
                EstatisticasGerais = await ObterEstatisticasPropostaEspecifica(propostaId),
                VisualizacoesPorDia = await ObterVisualizacoesPorDia(propostaId),
                DispositivosENavegadores = await ObterDispositivosENavegadores(propostaId),
                InteracoesUsuarios = await ObterInteracoesUsuarios(propostaId),
                MapaVisualizacoes = await ObterMapaVisualizacoes(propostaId),
                TemposPorSessao = await ObterTemposPorSessao(propostaId),
                ReferenciasTrafico = await ObterReferenciasTrafico(propostaId),
                AvaliacoesCliente = await ObterAvaliacoesCliente(propostaId)
            };

            return View(viewModel);
        }

        // APIs para dados em tempo real
        [HttpGet]
        public async Task<IActionResult> GetVisualizacoesGraficos(int dias = 30)
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var dataInicio = DateTime.Now.AddDays(-dias);

            var isMaster = SessaoIsMaster();
            var dados = await _context.PropostaVisualizacoes
                .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                    && v.DataHoraInicio >= dataInicio)
                .GroupBy(v => v.DataHoraInicio.Date)
                .Select(g => new
                {
                    Data = g.Key.ToString("yyyy-MM-dd"),
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos),
                    Interacoes = g.Count(v => v.ClicouEmail || v.ClicouWhatsApp)
                })
                .OrderBy(x => x.Data)
                .ToListAsync();

            return Json(dados);
        }

        [HttpGet]
        public async Task<IActionResult> GetEstatisticasTempoReal()
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var hoje = DateTime.Today;

            var stats = new
            {
                VisualizacoesHoje = await _context.PropostaVisualizacoes
                    .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                        && v.DataHoraInicio >= hoje)
                    .CountAsync(),

                TempoMedioHoje = await _context.PropostaVisualizacoes
                    .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                        && v.DataHoraInicio >= hoje)
                    .AverageAsync(v => (double?)v.TempoVisualizacaoSegundos) ?? 0,

                InteracoesHoje = await _context.PropostaVisualizacoes
                    .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                        && v.DataHoraInicio >= hoje)
                    .CountAsync(v => v.ClicouEmail || v.ClicouWhatsApp),

                PropostasAtivasHoje = await _context.Propostas
                    .Where(p => (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                        && p.LinkPublicoAtivo)
                    .CountAsync()
            };

            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetMapaLocalizacoes()
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var localizacoes = await _context.PropostaVisualizacoes
                .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                    && v.Latitude.HasValue && v.Longitude.HasValue)
                .GroupBy(v => new { v.Cidade, v.Estado, v.Pais, v.Latitude, v.Longitude })
                .Select(g => new
                {
                    Cidade = g.Key.Cidade ?? "Desconhecida",
                    Estado = g.Key.Estado ?? "",
                    Pais = g.Key.Pais ?? "Desconhecido",
                    Latitude = g.Key.Latitude,
                    Longitude = g.Key.Longitude,
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos)
                })
                .OrderByDescending(x => x.Visualizacoes)
                .Take(50)
                .ToListAsync();

            return Json(localizacoes);
        }

        [HttpGet]
        public async Task<IActionResult> GetPropostaEstatisticas(Guid propostaId)
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            // Verificar propriedade (respeitando hierarquia)
            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null) return NotFound();

            var stats = await ObterEstatisticasPropostaEspecifica(propostaId);
            return Json(stats);
        }

        // Métodos auxiliares privados - CORRIGIDOS
        private async Task<List<VisualizacaoDiariaViewModel>> ObterVisualizacoesUltimos30Dias(Guid usuarioId, bool isMaster)
        {
            var dataInicio = DateTime.Now.AddDays(-30);

            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                    && v.DataHoraInicio >= dataInicio)
                .GroupBy(v => v.DataHoraInicio.Date)
                .Select(g => new VisualizacaoDiariaViewModel
                {
                    Data = g.Key,
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos),
                    Interacoes = g.Count(v => v.ClicouEmail || v.ClicouWhatsApp)
                })
                .OrderBy(x => x.Data)
                .ToListAsync();
        }

        private async Task<List<PropostaPopularViewModel>> ObterPropostasPopulares(Guid usuarioId, bool isMaster)
        {
            var propostas = await _context.Propostas
                .Where(p => isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                .Select(p => new
                {
                    p.Id,
                    p.Titulo,
                    Visualizacoes = p.PropostaVisualizacoes.Count(),
                    TempoMedio = p.PropostaVisualizacoes.Any() ?
                        p.PropostaVisualizacoes.Average(v => v.TempoVisualizacaoSegundos) : 0,
                    Interacoes = p.PropostaVisualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp)
                })
                .ToListAsync();

            return propostas.Select(p => new PropostaPopularViewModel
            {
                PropostaId = p.Id,
                Titulo = p.Titulo,
                TotalVisualizacoes = p.Visualizacoes,
                TempoMedio = p.TempoMedio,
                TaxaInteracao = p.Visualizacoes > 0 ? (double)p.Interacoes / p.Visualizacoes * 100 : 0
            })
            .OrderByDescending(x => x.TotalVisualizacoes)
            .Take(10)
            .ToList();
        }

        private async Task<EstatisticasGeraisViewModel> ObterEstatisticasGerais(Guid usuarioId, bool isMaster)
        {
            var visualizacoes = await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                .ToListAsync();

            return new EstatisticasGeraisViewModel
            {
                TempoMedioVisualizacao = visualizacoes.Any() ? visualizacoes.Average(v => v.TempoVisualizacaoSegundos) : 0,
                ScrollMedioPercentual = visualizacoes.Any() ? visualizacoes.Average(v => v.ScrollMaximoPercentual) : 0,
                TaxaInteracao = visualizacoes.Any() ?
                    (double)visualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp) / visualizacoes.Count * 100 : 0,
                TotalCliques = visualizacoes.Sum(v => v.NumeroCliques),
                VisualizacoesUnicasPorDispositivo = visualizacoes
                    .Where(v => !string.IsNullOrEmpty(v.DeviceFingerprint))
                    .Select(v => v.DeviceFingerprint)
                    .Distinct()
                    .Count()
            };
        }

        private async Task<List<LocalizacaoAcessoViewModel>> ObterLocalizacoesAcessos(Guid usuarioId, bool isMaster)
        {
            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                    && !string.IsNullOrEmpty(v.Pais))
                .GroupBy(v => new { v.Pais, v.Estado, v.Cidade })
                .Select(g => new LocalizacaoAcessoViewModel
                {
                    Pais = g.Key.Pais,
                    Estado = g.Key.Estado ?? "",
                    Cidade = g.Key.Cidade ?? "",
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos)
                })
                .OrderByDescending(x => x.Visualizacoes)
                .Take(20)
                .ToListAsync();
        }

        private async Task<List<DispositivoAcessoViewModel>> ObterDispositivosAcessos(Guid usuarioId, bool isMaster)
        {
            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId)
                    && !string.IsNullOrEmpty(v.TipoDispositivo))
                .GroupBy(v => new { v.TipoDispositivo, v.Navegador, v.SistemaOperacional })
                .Select(g => new DispositivoAcessoViewModel
                {
                    TipoDispositivo = g.Key.TipoDispositivo,
                    Navegador = g.Key.Navegador ?? "",
                    SistemaOperacional = g.Key.SistemaOperacional ?? "",
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos)
                })
                .OrderByDescending(x => x.Visualizacoes)
                .ToListAsync();
        }

        private async Task<EstatisticasPropostaViewModel> ObterEstatisticasPropostaEspecifica(Guid propostaId)
        {
            var visualizacoes = await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId)
                .ToListAsync();

            if (!visualizacoes.Any())
            {
                return new EstatisticasPropostaViewModel
                {
                    TotalVisualizacoes = 0,
                    VisualizacoesUnicas = 0,
                    TempoMedioSegundos = 0,
                    ScrollMedioPercentual = 0,
                    TotalCliques = 0,
                    CliquesWhatsApp = 0,
                    CliquesEmail = 0,
                    TaxaInteracao = 0,
                    UltimaVisualizacao = null,
                    PrimeiraVisualizacao = null
                };
            }

            return new EstatisticasPropostaViewModel
            {
                TotalVisualizacoes = visualizacoes.Count,
                VisualizacoesUnicas = visualizacoes
                    .Where(v => !string.IsNullOrEmpty(v.DeviceFingerprint))
                    .Select(v => v.DeviceFingerprint)
                    .Distinct()
                    .Count(),
                TempoMedioSegundos = visualizacoes.Average(v => v.TempoVisualizacaoSegundos),
                ScrollMedioPercentual = visualizacoes.Average(v => v.ScrollMaximoPercentual),
                TotalCliques = visualizacoes.Sum(v => v.NumeroCliques),
                CliquesWhatsApp = visualizacoes.Count(v => v.ClicouWhatsApp),
                CliquesEmail = visualizacoes.Count(v => v.ClicouEmail),
                TaxaInteracao = (double)visualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp) / visualizacoes.Count * 100,
                UltimaVisualizacao = visualizacoes.Max(v => v.DataHoraInicio),
                PrimeiraVisualizacao = visualizacoes.Min(v => v.DataHoraInicio)
            };
        }

        private async Task<List<VisualizacaoDiariaViewModel>> ObterVisualizacoesPorDia(Guid propostaId)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId)
                .GroupBy(v => v.DataHoraInicio.Date)
                .Select(g => new VisualizacaoDiariaViewModel
                {
                    Data = g.Key,
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos),
                    Interacoes = g.Count(v => v.ClicouEmail || v.ClicouWhatsApp)
                })
                .OrderBy(x => x.Data)
                .ToListAsync();
        }

        private async Task<List<DispositivoEstatisticaViewModel>> ObterDispositivosENavegadores(Guid propostaId)
        {
            var dados = await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId && !string.IsNullOrEmpty(v.TipoDispositivo))
                .GroupBy(v => new { v.TipoDispositivo, v.Navegador })
                .Select(g => new DispositivoEstatisticaViewModel
                {
                    Categoria = g.Key.TipoDispositivo,
                    Subcategoria = g.Key.Navegador ?? "Desconhecido",
                    Quantidade = g.Count(),
                    Percentual = 0 // Será calculado após buscar todos os dados
                })
                .OrderByDescending(x => x.Quantidade)
                .ToListAsync();

            // Calcular percentuais
            var total = dados.Sum(d => d.Quantidade);
            if (total > 0)
            {
                foreach (var item in dados)
                {
                    item.Percentual = (double)item.Quantidade / total * 100;
                }
            }

            return dados;
        }

        private async Task<List<InteracaoUsuarioViewModel>> ObterInteracoesUsuarios(Guid propostaId)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId)
                .OrderByDescending(v => v.DataHoraInicio)
                .Take(50)
                .Select(v => new InteracaoUsuarioViewModel
                {
                    SessionToken = v.SessionToken,
                    DataHoraInicio = v.DataHoraInicio,
                    DataHoraFim = v.DataHoraFim,
                    TempoSegundos = v.TempoVisualizacaoSegundos,
                    ScrollPercentual = v.ScrollMaximoPercentual,
                    Cliques = v.NumeroCliques,
                    ClicouWhatsApp = v.ClicouWhatsApp,
                    ClicouEmail = v.ClicouEmail,
                    TipoDispositivo = v.TipoDispositivo,
                    Navegador = v.Navegador,
                    Cidade = v.Cidade,
                    Estado = v.Estado,
                    Pais = v.Pais
                })
                .ToListAsync();
        }

        private async Task<List<LocalizacaoVisualizacaoViewModel>> ObterMapaVisualizacoes(Guid propostaId)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId &&
                           v.Latitude.HasValue && v.Longitude.HasValue)
                .GroupBy(v => new { v.Latitude, v.Longitude, v.Cidade, v.Estado, v.Pais })
                .Select(g => new LocalizacaoVisualizacaoViewModel
                {
                    Latitude = g.Key.Latitude.Value,
                    Longitude = g.Key.Longitude.Value,
                    Cidade = g.Key.Cidade ?? "Desconhecida",
                    Estado = g.Key.Estado ?? "",
                    Pais = g.Key.Pais ?? "Desconhecido",
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos)
                })
                .ToListAsync();
        }

        private async Task<List<TempoSessaoViewModel>> ObterTemposPorSessao(Guid propostaId)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId)
                .Select(v => new TempoSessaoViewModel
                {
                    SessionToken = v.SessionToken,
                    TempoSegundos = v.TempoVisualizacaoSegundos,
                    DataHora = v.DataHoraInicio,
                    ScrollPercentual = v.ScrollMaximoPercentual
                })
                .OrderBy(x => x.TempoSegundos)
                .ToListAsync();
        }

        private async Task<List<ReferenciaTrafegoViewModel>> ObterReferenciasTrafico(Guid propostaId)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId && !string.IsNullOrEmpty(v.TipoReferenciador))
                .GroupBy(v => v.TipoReferenciador)
                .Select(g => new ReferenciaTrafegoViewModel
                {
                    TipoReferencia = g.Key,
                    Visualizacoes = g.Count(),
                    TempoMedio = g.Average(v => v.TempoVisualizacaoSegundos),
                    TaxaInteracao = (double)g.Count(v => v.ClicouEmail || v.ClicouWhatsApp) / g.Count() * 100
                })
                .OrderByDescending(x => x.Visualizacoes)
                .ToListAsync();
        }

        private async Task<AvaliacoesClienteAnalyticsViewModel> ObterAvaliacoesCliente(Guid propostaId)
        {
            var avaliacoes = await _context.AvaliacoesCliente
                .Where(a => a.PropostaId == propostaId)
                .AsNoTracking()
                .ToListAsync();

            if (!avaliacoes.Any())
                return new AvaliacoesClienteAnalyticsViewModel();

            // Batch-fetch nomes por tipo
            var hospIds = avaliacoes.Where(a => a.TipoItem == TipoItemAvaliacao.Hospedagem).Select(a => a.ItemId).ToHashSet();
            var acmdIds = avaliacoes.Where(a => a.TipoItem == TipoItemAvaliacao.Acomodacao).Select(a => a.ItemId).ToHashSet();
            var expIds  = avaliacoes.Where(a => a.TipoItem == TipoItemAvaliacao.Experiencia).Select(a => a.ItemId).ToHashSet();

            var hospNomes = hospIds.Any()
                ? await _context.Hospedagens.Where(h => hospIds.Contains(h.Id))
                    .Select(h => new { h.Id, h.Nome }).AsNoTracking()
                    .ToDictionaryAsync(h => h.Id, h => h.Nome)
                : new Dictionary<Guid, string>();

            var acmdNomes = acmdIds.Any()
                ? await _context.Acomodacoes.Where(a => acmdIds.Contains(a.Id))
                    .Select(a => new { a.Id, a.Nome }).AsNoTracking()
                    .ToDictionaryAsync(a => a.Id, a => a.Nome)
                : new Dictionary<Guid, string>();

            var expNomes = expIds.Any()
                ? await _context.Experiencias.Where(e => expIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.TipoPasseio }).AsNoTracking()
                    .ToDictionaryAsync(e => e.Id, e => e.TipoPasseio)
                : new Dictionary<Guid, string>();

            static string TipoNome(TipoItemAvaliacao tipo) => tipo switch
            {
                TipoItemAvaliacao.Hospedagem  => "Hospedagem",
                TipoItemAvaliacao.Acomodacao  => "Acomodação",
                TipoItemAvaliacao.Experiencia => "Experiência",
                _                             => tipo.ToString()
            };

            string ResolverNome(AvaliacaoCliente a) => a.TipoItem switch
            {
                TipoItemAvaliacao.Hospedagem  => hospNomes.GetValueOrDefault(a.ItemId, "Item não encontrado"),
                TipoItemAvaliacao.Acomodacao  => acmdNomes.GetValueOrDefault(a.ItemId, "Item não encontrado"),
                TipoItemAvaliacao.Experiencia => expNomes.GetValueOrDefault(a.ItemId, "Item não encontrado"),
                _                             => "Item não encontrado"
            };

            var itens = avaliacoes
                .OrderByDescending(a => a.Favorito)
                .ThenByDescending(a => a.Nota)
                .ThenByDescending(a => a.DataCriacao)
                .ThenBy(a => a.TipoItem)
                .Select(a => new AvaliacaoClienteResumoViewModel
                {
                    Id           = a.Id,
                    TipoItemNome = TipoNome(a.TipoItem),
                    ItemId       = a.ItemId,
                    ItemNome     = ResolverNome(a),
                    Nota         = a.Nota,
                    Comentario   = a.Comentario,
                    Favorito     = a.Favorito,
                    DataCriacao  = a.DataCriacao
                })
                .ToList();

            var notas = avaliacoes.Select(a => a.Nota).ToList();
            return new AvaliacoesClienteAnalyticsViewModel
            {
                TotalAvaliacoes  = avaliacoes.Count,
                TotalFavoritos   = avaliacoes.Count(a => a.Favorito),
                NotaMedia        = notas.Any() ? Math.Round((decimal)notas.Average(), 1) : null,
                TotalComentarios = avaliacoes.Count(a => !string.IsNullOrWhiteSpace(a.Comentario)),
                Itens            = itens
            };
        }

        private async Task<List<PropostaRadarViewModel>> ObterPropostasRadar(Guid usuarioId, bool isMaster)
        {
            var lista = await _context.Propostas
                .Where(p =>
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                    && p.PropostaVisualizacoes.Any())
                .Select(p => new PropostaRadarViewModel
                {
                    PropostaId = p.Id,
                    Titulo = p.Titulo,
                    ClienteNome = p.Cliente != null ? p.Cliente.Nome : null,
                    ClienteId = p.ClienteId,
                    DestinoNome = p.Destinos.OrderBy(d => d.Ordem).Select(d => d.Nome).FirstOrDefault(),
                    StatusProposta = p.StatusProposta,
                    TotalVisualizacoes = p.PropostaVisualizacoes.Count(),
                    UltimaVisualizacao = p.PropostaVisualizacoes.Max(v => (DateTime?)v.DataHoraInicio),
                    LinkPublicoAtivo = p.LinkPublicoAtivo,
                    TaxaInteracao = p.PropostaVisualizacoes.Count() > 0
                        ? (double)p.PropostaVisualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp)
                          / (double)p.PropostaVisualizacoes.Count() * 100
                        : 0
                })
                .OrderByDescending(x => x.UltimaVisualizacao)
                .ThenByDescending(x => x.TotalVisualizacoes)
                .Take(25)
                .ToListAsync();

            // Batch: contagem de avaliações por proposta (evita N+1)
            var ids = lista.Select(x => x.PropostaId).ToList();
            var avaliacoesPorProposta = await _context.AvaliacoesCliente
                .AsNoTracking()
                .Where(a => ids.Contains(a.PropostaId))
                .GroupBy(a => a.PropostaId)
                .Select(g => new { PropostaId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.PropostaId, x => x.Total);

            foreach (var item in lista)
                item.TotalAvaliacoes = avaliacoesPorProposta.GetValueOrDefault(item.PropostaId, 0);

            return lista
                .OrderByDescending(x => x.UltimaVisualizacao)
                .ThenByDescending(x => x.TotalVisualizacoes)
                .ThenByDescending(x => x.TotalAvaliacoes)
                .ToList();
        }

        private async Task<List<AtividadeRecenteViewModel>> ObterAtividadeRecente(Guid usuarioId, bool isMaster)
        {
            return await _context.PropostaVisualizacoes
                .Where(v => isMaster
                    ? v.Proposta.UsuarioMasterId == usuarioId
                    : v.Proposta.UsuarioResponsavelId == usuarioId)
                .OrderByDescending(v => v.DataHoraInicio)
                .Take(20)
                .Select(v => new AtividadeRecenteViewModel
                {
                    PropostaId = v.PropostaId,
                    PropostaTitulo = v.Proposta.Titulo,
                    ClienteNome = v.Proposta.Cliente != null ? v.Proposta.Cliente.Nome : null,
                    ClienteId = v.Proposta.ClienteId,
                    DataHoraVisualizacao = v.DataHoraInicio,
                    TipoDispositivo = v.TipoDispositivo,
                    Cidade = v.Cidade,
                    Pais = v.Pais
                })
                .ToListAsync();
        }

        private async Task<List<OportunidadeFollowUpViewModel>> ObterOportunidadesFollowUp(Guid usuarioId, bool isMaster)
        {
            var seteAtras = DateTime.Now.AddDays(-7);
            var trintaAtras = DateTime.Now.AddDays(-30);

            var dados = await _context.Propostas
                .Where(p =>
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                    && p.PropostaVisualizacoes.Any(v => v.DataHoraInicio >= trintaAtras)
                    && p.StatusProposta != StatusProposta.Aprovada
                    && p.StatusProposta != StatusProposta.Cancelada)
                .Select(p => new
                {
                    p.Id,
                    p.Titulo,
                    ClienteNome = p.Cliente != null ? p.Cliente.Nome : null,
                    p.ClienteId,
                    p.StatusProposta,
                    TotalVisualizacoes = p.PropostaVisualizacoes.Count(),
                    VisualizacoesRecentes = p.PropostaVisualizacoes.Count(v => v.DataHoraInicio >= seteAtras),
                    UltimaVisualizacao = p.PropostaVisualizacoes.Max(v => (DateTime?)v.DataHoraInicio)
                })
                .OrderByDescending(x => x.UltimaVisualizacao)
                .Take(10)
                .ToListAsync();

            return dados.Select(d =>
            {
                string motivo;
                if (d.VisualizacoesRecentes > 1)
                    motivo = $"Visualizou {d.VisualizacoesRecentes}× nos últimos 7 dias. Ótimo momento para contato.";
                else if (d.UltimaVisualizacao >= DateTime.Now.AddDays(-1))
                    motivo = "Proposta visualizada hoje. Bom momento para fazer follow-up.";
                else if (d.UltimaVisualizacao >= seteAtras)
                    motivo = $"Proposta visualizada {FormatarTempoRelativo(d.UltimaVisualizacao!.Value)}. Cliente demonstrou interesse recente.";
                else
                    motivo = $"Proposta visualizada {d.TotalVisualizacoes}× no último mês sem resposta.";

                return new OportunidadeFollowUpViewModel
                {
                    PropostaId = d.Id,
                    PropostaTitulo = d.Titulo,
                    ClienteNome = d.ClienteNome,
                    ClienteId = d.ClienteId,
                    TotalVisualizacoes = d.TotalVisualizacoes,
                    UltimaVisualizacao = d.UltimaVisualizacao,
                    StatusProposta = d.StatusProposta,
                    MotivoSugestao = motivo
                };
            }).ToList();
        }

        private static string FormatarTempoRelativo(DateTime dataHora)
        {
            var diff = DateTime.Now - dataHora;
            if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes}min";
            if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours}h";
            if (diff.TotalDays < 2) return "ontem";
            if (diff.TotalDays < 7) return $"há {(int)diff.TotalDays} dias";
            return dataHora.ToString("dd/MM");
        }

        [HttpPost]
        public IActionResult SalvarDestino(DestinoViewModel destino)
        {
            // Recupera a lista da sessão (ou do banco, se preferir)
            var destinos = HttpContext.Session.GetObjectFromJson<List<DestinoViewModel>>("Destinos") ?? new List<DestinoViewModel>();

            if (destino.Id == null || destino.Id == Guid.Empty)
            {
                destino.Id = Guid.NewGuid();
                destinos.Add(destino);
            }
            else
            {
                var existente = destinos.FirstOrDefault(d => d.Id == destino.Id);
                if (existente != null)
                {
                    existente.Nome = destino.Nome;
                    // Atualize outros campos
                }
            }

            HttpContext.Session.SetObjectAsJson("Destinos", destinos);
            return PartialView("_DestinosLista", destinos);
        }

        [HttpPost]
        public IActionResult RemoverDestino(Guid id)
        {
            var destinos = HttpContext.Session.GetObjectFromJson<List<DestinoViewModel>>("Destinos") ?? new List<DestinoViewModel>();
            destinos = destinos.Where(d => d.Id != id).ToList();
            HttpContext.Session.SetObjectAsJson("Destinos", destinos);
            return PartialView("_DestinosLista", destinos);
        }
    }

    public class DestinoViewModel
    {
        public Guid? Id { get; set; }
        public string Nome { get; set; }
        // Adicione outros campos necessários
    }

    public class PropostaWizardViewModel
    {
        // ... outros campos
        public List<DestinoViewModel> Destinos { get; set; } = new List<DestinoViewModel>();
    }

    public static class SessionExtensions
    {
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }
}