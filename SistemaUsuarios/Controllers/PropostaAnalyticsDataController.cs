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
                // Dados do usuário
                UsuarioId = usuarioId,

                // Estatísticas gerais
                TotalPropostas = await _context.Propostas.CountAsync(p => p.UsuarioId == usuarioId),
                TotalVisualizacoes = await _context.PropostaVisualizacoes
                    .Where(v => v.Proposta.UsuarioId == usuarioId)
                    .CountAsync(),

                // Dados dos últimos 30 dias
                VisualizacoesUltimos30Dias = await ObterVisualizacoesUltimos30Dias(usuarioId),
                PropostasPopulares = await ObterPropostasPopulares(usuarioId),
                EstatisticasGerais = await ObterEstatisticasGerais(usuarioId),
                LocalizacoesAcessos = await ObterLocalizacoesAcessos(usuarioId),
                DispositivosAcessos = await ObterDispositivosAcessos(usuarioId)
            };

            return View(viewModel);
        }

        // DASHBOARD ESPECÍFICO - Nível 2
        public async Task<IActionResult> PropostaDetalhada(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            // Verificar se a proposta pertence ao usuário
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

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

        // Métodos auxiliares privados

        private async Task<List<VisualizacaoDiariaViewModel>> ObterVisualizacoesUltimos30Dias(Guid usuarioId)
        {
            var dataInicio = DateTime.Now.AddDays(-30);

            return await _context.PropostaVisualizacoes
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
                .ToListAsync();

            var result = new List<PropostaPopularViewModel>();

            foreach (var proposta in propostas)
            {
                var visualizacoes = await _context.PropostaVisualizacoes
                    .Where(v => v.PropostaId == proposta.Id)
                    .ToListAsync();

                var totalVisualizacoes = visualizacoes.Count;
                var tempoMedio = visualizacoes.Any() ? visualizacoes.Average(v => v.TempoVisualizacaoSegundos) : 0;
                var interacoes = visualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp);
                var taxaInteracao = totalVisualizacoes > 0 ? (double)interacoes / totalVisualizacoes * 100 : 0;

                result.Add(new PropostaPopularViewModel
                {
                    PropostaId = proposta.Id,
                    Titulo = proposta.Titulo,
                    TotalVisualizacoes = totalVisualizacoes,
                    TempoMedio = tempoMedio,
                    TaxaInteracao = taxaInteracao
                });
            }

            return result.OrderByDescending(x => x.TotalVisualizacoes).Take(10).ToList();
        }

        private async Task<EstatisticasGeraisViewModel> ObterEstatisticasGerais(Guid usuarioId)
        {
            var visualizacoes = await _context.PropostaVisualizacoes
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
                    .GroupBy(v => v.DeviceFingerprint)
                    .Count()
            };
        }

        private async Task<List<LocalizacaoAcessoViewModel>> ObterLocalizacoesAcessos(Guid usuarioId)
        {
            return await _context.PropostaVisualizacoes
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
                return new EstatisticasPropostaViewModel();
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
            return await _context.PropostaVisualizacoes
                .Where(v => v.PropostaId == propostaId && !string.IsNullOrEmpty(v.TipoDispositivo))
                .GroupBy(v => new { v.TipoDispositivo, v.Navegador })
                .Select(g => new DispositivoEstatisticaViewModel
                {
                    Categoria = g.Key.TipoDispositivo,
                    Subcategoria = g.Key.Navegador ?? "Desconhecido",
                    Quantidade = g.Count(),
                    Percentual = 0 // Será calculado na view
                })
                .OrderByDescending(x => x.Quantidade)
                .ToListAsync();
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

    // ViewModels para o Dashboard

    public class DashboardAnalyticsViewModel
    {
        public Guid UsuarioId { get; set; }
        public int TotalPropostas { get; set; }
        public int TotalVisualizacoes { get; set; }
        public List<VisualizacaoDiariaViewModel> VisualizacoesUltimos30Dias { get; set; } = new();
        public List<PropostaPopularViewModel> PropostasPopulares { get; set; } = new();
        public EstatisticasGeraisViewModel EstatisticasGerais { get; set; } = new();
        public List<LocalizacaoAcessoViewModel> LocalizacoesAcessos { get; set; } = new();
        public List<DispositivoAcessoViewModel> DispositivosAcessos { get; set; } = new();
    }

    public class PropostaAnalyticsDetalhadaViewModel
    {
        public Proposta Proposta { get; set; }
        public EstatisticasPropostaViewModel EstatisticasGerais { get; set; } = new();
        public List<VisualizacaoDiariaViewModel> VisualizacoesPorDia { get; set; } = new();
        public List<DispositivoEstatisticaViewModel> DispositivosENavegadores { get; set; } = new();
        public List<InteracaoUsuarioViewModel> InteracoesUsuarios { get; set; } = new();
        public List<LocalizacaoVisualizacaoViewModel> MapaVisualizacoes { get; set; } = new();
        public List<TempoSessaoViewModel> TemposPorSessao { get; set; } = new();
        public List<ReferenciaTrafegoViewModel> ReferenciasTrafico { get; set; } = new();
    }

    // ViewModels auxiliares
    public class VisualizacaoDiariaViewModel
    {
        public DateTime Data { get; set; }
        public int Visualizacoes { get; set; }
        public double TempoMedio { get; set; }
        public int Interacoes { get; set; }
    }

    public class PropostaPopularViewModel
    {
        public Guid PropostaId { get; set; }
        public string Titulo { get; set; }
        public int TotalVisualizacoes { get; set; }
        public double TempoMedio { get; set; }
        public double TaxaInteracao { get; set; }
    }

    public class EstatisticasGeraisViewModel
    {
        public double TempoMedioVisualizacao { get; set; }
        public double ScrollMedioPercentual { get; set; }
        public double TaxaInteracao { get; set; }
        public int TotalCliques { get; set; }
        public int VisualizacoesUnicasPorDispositivo { get; set; }
    }

    public class LocalizacaoAcessoViewModel
    {
        public string Pais { get; set; }
        public string Estado { get; set; }
        public string Cidade { get; set; }
        public int Visualizacoes { get; set; }
        public double TempoMedio { get; set; }
    }

    public class DispositivoAcessoViewModel
    {
        public string TipoDispositivo { get; set; }
        public string Navegador { get; set; }
        public string SistemaOperacional { get; set; }
        public int Visualizacoes { get; set; }
        public double TempoMedio { get; set; }
    }

    public class DispositivoEstatisticaViewModel
    {
        public string Categoria { get; set; }
        public string Subcategoria { get; set; }
        public int Quantidade { get; set; }
        public double Percentual { get; set; }
    }

    public class InteracaoUsuarioViewModel
    {
        public string SessionToken { get; set; }
        public DateTime DataHoraInicio { get; set; }
        public DateTime? DataHoraFim { get; set; }
        public int TempoSegundos { get; set; }
        public int ScrollPercentual { get; set; }
        public int Cliques { get; set; }
        public bool ClicouWhatsApp { get; set; }
        public bool ClicouEmail { get; set; }
        public string TipoDispositivo { get; set; }
        public string Navegador { get; set; }
        public string Cidade { get; set; }
        public string Estado { get; set; }
        public string Pais { get; set; }
    }

    public class LocalizacaoVisualizacaoViewModel
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Cidade { get; set; }
        public string Estado { get; set; }
        public string Pais { get; set; }
        public int Visualizacoes { get; set; }
        public double TempoMedio { get; set; }
    }

    public class TempoSessaoViewModel
    {
        public string SessionToken { get; set; }
        public int TempoSegundos { get; set; }
        public DateTime DataHora { get; set; }
        public int ScrollPercentual { get; set; }
    }

    public class ReferenciaTrafegoViewModel
    {
        public string TipoReferencia { get; set; }
        public int Visualizacoes { get; set; }
        public double TempoMedio { get; set; }
        public double TaxaInteracao { get; set; }
    }
}