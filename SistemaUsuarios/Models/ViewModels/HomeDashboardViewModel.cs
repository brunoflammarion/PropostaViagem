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

        // Fila de atenção (max 8)
        public List<AttentionItem> FilaAtencao { get; set; } = new();

        // Continue de onde parou (max 6)
        public List<ContinueItem> ContinueList { get; set; } = new();

        // Viagens e agenda
        public List<TravelAgendaItem> ViagensAgenda { get; set; } = new();

        // Captação / página pública
        public CaptacaoHomeInfo? Captacao { get; set; }
    }

    public class AttentionItem
    {
        public string TipoIcon { get; set; } = "";
        public string TipoBadge { get; set; } = "";
        public string TipoCss { get; set; } = "";
        public string NomeContato { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Contexto { get; set; } = "";
        public string Url { get; set; } = "";
        public string AcaoLabel { get; set; } = "";
        public int Prioridade { get; set; }
        public DateTime SecondarySortKey { get; set; }
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
