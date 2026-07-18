namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    public class AgenciaListViewModel
    {
        public List<AgenciaListItem> Agencias { get; set; } = new();
        public string? FiltroStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class AgenciaListItem
    {
        public Guid Id { get; set; }
        public string NomeAgencia { get; set; } = string.Empty;
        public string? SlugAgencia { get; set; }
        public string NomeMaster { get; set; } = string.Empty;
        public StatusUsuario Status { get; set; }
        public DateTime DataCriacao { get; set; }
        public int TotalAssociados { get; set; }
        public int TotalPropostas { get; set; }
        public int TotalClientes { get; set; }
        public int TotalLeads { get; set; }
        public int TotalVisualizacoes { get; set; }
        public DateTime? UltimaAtividade { get; set; }

        // ── Consumo IA ────────────────────────────────────────────────────────
        public decimal ConsumoMensalUsd { get; set; }
        public decimal? LimiteMensalUsd { get; set; }
        public AiModoControle ModoControleIa { get; set; } = AiModoControle.Monitoramento;
        public int PercentualConsumoIa => LimiteMensalUsd.HasValue && LimiteMensalUsd > 0
            ? (int)Math.Min(100, Math.Round(ConsumoMensalUsd / LimiteMensalUsd.Value * 100))
            : 0;

        public string StatusLabel => Status switch
        {
            StatusUsuario.Ativo => "Ativa",
            StatusUsuario.Bloqueado => "Bloqueada",
            StatusUsuario.Inativo => "Inativa",
            StatusUsuario.Novo => "Nova",
            _ => "–"
        };

        public string StatusCss => Status switch
        {
            StatusUsuario.Ativo => "success",
            StatusUsuario.Bloqueado => "danger",
            StatusUsuario.Inativo => "secondary",
            StatusUsuario.Novo => "warning",
            _ => "secondary"
        };
    }
}
