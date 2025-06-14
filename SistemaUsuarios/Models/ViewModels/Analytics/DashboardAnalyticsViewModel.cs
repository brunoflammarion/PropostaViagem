using SistemaUsuarios.Models;

namespace SistemaUsuarios.Models.ViewModels.Analytics
{
    // DASHBOARD GERAL - NÍVEL 1
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

    // DASHBOARD ESPECÍFICO - NÍVEL 2
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
        public AnaliseAgenteViewModel AnaliseAgente { get; set; } = new();
    }

    // ANÁLISE PARA O AGENTE
    public class AnaliseAgenteViewModel
    {
        public double TempoMedioVisualizacao { get; set; }
        public double ScrollMedioPercentual { get; set; }
        public double TaxaInteracao { get; set; }
        public int TotalCliques { get; set; }
        public int VisualizacoesUnicas { get; set; }
        public List<DispositivoPopularViewModel> DispositivosMaisComuns { get; set; } = new();
        public List<NavegadorPopularViewModel> NavegadoresMaisComuns { get; set; } = new();
        public List<LocalizacaoPopularViewModel> LocalizacoesPrincipais { get; set; } = new();
        public List<HorarioPicoViewModel> HorariosPico { get; set; } = new();
        public double TaxaEngajamento { get; set; }
    }

    public class DispositivoPopularViewModel
    {
        public string Tipo { get; set; }
        public int Quantidade { get; set; }
    }

    public class NavegadorPopularViewModel
    {
        public string Navegador { get; set; }
        public int Quantidade { get; set; }
    }

    public class LocalizacaoPopularViewModel
    {
        public string Localizacao { get; set; }
        public int Quantidade { get; set; }
    }

    public class HorarioPicoViewModel
    {
        public int Hora { get; set; }
        public int Quantidade { get; set; }
    }

    // VIEWMODELS AUXILIARES
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

    public class EstatisticasPropostaViewModel
    {
        public int TotalVisualizacoes { get; set; }
        public int VisualizacoesUnicas { get; set; }
        public double TempoMedioSegundos { get; set; }
        public double ScrollMedioPercentual { get; set; }
        public int TotalCliques { get; set; }
        public int CliquesWhatsApp { get; set; }
        public int CliquesEmail { get; set; }
        public double TaxaInteracao { get; set; }
        public DateTime? UltimaVisualizacao { get; set; }
        public DateTime? PrimeiraVisualizacao { get; set; }
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