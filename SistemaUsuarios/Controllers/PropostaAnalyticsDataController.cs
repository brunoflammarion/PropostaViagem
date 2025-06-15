using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels.Analytics;
using System.Text.Json;

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

        // DASHBOARD GERAL - Nível 1
        public async Task<IActionResult> Dashboard()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var viewModel = new DashboardAnalyticsViewModel
            {
                UsuarioId = usuarioId,
                TotalPropostas = await _context.Propostas.CountAsync(p => p.UsuarioId == usuarioId),
                TotalVisualizacoes = await _context.PropostaVisualizacoes
                    .Where(v => v.Proposta.UsuarioId == usuarioId)
                    .CountAsync(),

                VisualizacoesUltimos30Dias = await ObterVisualizacoesUltimos30Dias(usuarioId),
                PropostasPopulares = await ObterPropostasPopulares(usuarioId),
                EstatisticasGerais = await ObterEstatisticasGerais(usuarioId),
                LocalizacoesAcessos = await ObterLocalizacoesAcessos(usuarioId),
                DispositivosAcessos = await ObterDispositivosAcessos(usuarioId)
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

            // Verificar se a proposta pertence ao usuário
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

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
                ReferenciasTrafico = await ObterReferenciasTrafico(propostaId)
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

            var dados = await _context.PropostaVisualizacoes
                .Where(v => v.Proposta.UsuarioId == usuarioId && v.DataHoraInicio >= dataInicio)
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
            var hoje = DateTime.Today;

            var stats = new
            {
                VisualizacoesHoje = await _context.PropostaVisualizacoes
                    .Where(v => v.Proposta.UsuarioId == usuarioId && v.DataHoraInicio >= hoje)
                    .CountAsync(),

                TempoMedioHoje = await _context.PropostaVisualizacoes
                    .Where(v => v.Proposta.UsuarioId == usuarioId && v.DataHoraInicio >= hoje)
                    .AverageAsync(v => (double?)v.TempoVisualizacaoSegundos) ?? 0,

                InteracoesHoje = await _context.PropostaVisualizacoes
                    .Where(v => v.Proposta.UsuarioId == usuarioId && v.DataHoraInicio >= hoje)
                    .CountAsync(v => v.ClicouEmail || v.ClicouWhatsApp),

                PropostasAtivasHoje = await _context.Propostas
                    .Where(p => p.UsuarioId == usuarioId && p.LinkPublicoAtivo)
                    .CountAsync()
            };

            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetMapaLocalizacoes()
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var localizacoes = await _context.PropostaVisualizacoes
                .Where(v => v.Proposta.UsuarioId == usuarioId &&
                           v.Latitude.HasValue && v.Longitude.HasValue)
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

            // Verificar propriedade
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null) return NotFound();

            var stats = await ObterEstatisticasPropostaEspecifica(propostaId);
            return Json(stats);
        }

        // Métodos auxiliares privados - CORRIGIDOS
        private async Task<List<VisualizacaoDiariaViewModel>> ObterVisualizacoesUltimos30Dias(Guid usuarioId)
        {
            var dataInicio = DateTime.Now.AddDays(-30);

            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => v.Proposta.UsuarioId == usuarioId && v.DataHoraInicio >= dataInicio)
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

        private async Task<List<PropostaPopularViewModel>> ObterPropostasPopulares(Guid usuarioId)
        {
            var propostas = await _context.Propostas
                .Where(p => p.UsuarioId == usuarioId)
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

        private async Task<EstatisticasGeraisViewModel> ObterEstatisticasGerais(Guid usuarioId)
        {
            var visualizacoes = await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => v.Proposta.UsuarioId == usuarioId)
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

        private async Task<List<LocalizacaoAcessoViewModel>> ObterLocalizacoesAcessos(Guid usuarioId)
        {
            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => v.Proposta.UsuarioId == usuarioId && !string.IsNullOrEmpty(v.Pais))
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

        private async Task<List<DispositivoAcessoViewModel>> ObterDispositivosAcessos(Guid usuarioId)
        {
            return await _context.PropostaVisualizacoes
                .Include(v => v.Proposta)
                .Where(v => v.Proposta.UsuarioId == usuarioId && !string.IsNullOrEmpty(v.TipoDispositivo))
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
    }
}