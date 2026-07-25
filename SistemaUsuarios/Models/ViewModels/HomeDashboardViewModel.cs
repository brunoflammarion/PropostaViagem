using SistemaUsuarios.Models;

namespace SistemaUsuarios.Models.ViewModels
{
    public class HomeDashboardViewModel
    {
        public string NomeUsuario { get; set; } = "";
        public string? FotoPath { get; set; }

        // Cards principais
        public int LeadsNovos { get; set; }
        public int PropostasVisualizadas7d { get; set; }
        public int ViagensAndamento { get; set; }
        public int ViagensProximas15d { get; set; }

        // Fila de atenção — tabs de tarefa (max 5 exibidas por aba)
        public List<Tarefa> TarefasTodas      { get; set; } = new();
        public List<Tarefa> TarefasAtrasadas  { get; set; } = new();
        public List<Tarefa> TarefasHoje       { get; set; } = new();
        public List<Tarefa> TarefasSemana     { get; set; } = new();
        public List<Tarefa> TarefasConcluidas { get; set; } = new();
        public int TotalTodas      { get; set; }
        public int TotalAtrasadas  { get; set; }
        public int TotalHoje       { get; set; }
        public int TotalSemana     { get; set; }

        // Continue de onde parou (max 6)
        public List<ContinueItem> ContinueList { get; set; } = new();

        // Viagens e agenda
        public List<TravelAgendaItem> ViagensAgenda { get; set; } = new();

        // Captação / página pública
        public CaptacaoHomeInfo? Captacao { get; set; }
    }

    public class ContinueItem
    {
        public string Tipo { get; set; } = "";
        public string TipoIcon { get; set; } = "";
        public string Nome { get; set; } = "";
        public string? Destino { get; set; }
        public string Status { get; set; } = "";
        public string StatusCss { get; set; } = "";
        public DateTime UltimaAtualizacao { get; set; }
        public string Url { get; set; } = "";
        public string AcaoLabel { get; set; } = "";
    }

    public class TravelAgendaItem
    {
        public string PropostaTitulo { get; set; } = "";
        public string? ClienteNome { get; set; }
        public string? Destino { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public bool EmAndamento { get; set; }
        public string Url { get; set; } = "";
    }

    public class CaptacaoHomeInfo
    {
        public bool IsActive { get; set; }
        public string? NomeAgencia { get; set; }
        public string? SlugAgencia { get; set; }
        public string? PublicUrl { get; set; }
    }
}
