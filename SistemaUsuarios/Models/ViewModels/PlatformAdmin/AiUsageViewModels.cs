namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    // ── Vista Global de Consumo IA ─────────────────────────────────────────────
    public class AiUsageGlobalViewModel
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public string MesLabel => new DateTime(Ano, Mes, 1).ToString("MMMM yyyy");

        public decimal CustoTotalUsd { get; set; }
        public long TotalChamadas { get; set; }
        public long TotalTokens { get; set; }
        public int AgenciasAtivas { get; set; }
        public int ChamadasBloqueadas { get; set; }

        public List<AiUsageAgenciaRow> Agencias { get; set; } = new();
        public List<AiUsageFuncRow> PorFuncionalidade { get; set; } = new();
        public List<AiUsageModeloRow> PorModelo { get; set; } = new();
    }

    public class AiUsageAgenciaRow
    {
        public Guid AgenciaId { get; set; }
        public string NomeAgencia { get; set; } = "";
        public decimal CustoUsd { get; set; }
        public long Chamadas { get; set; }
        public long Tokens { get; set; }
        public decimal? LimiteUsd { get; set; }
        public AiModoControle ModoControle { get; set; }
        public int Percentual => LimiteUsd.HasValue && LimiteUsd > 0
            ? (int)Math.Min(100, Math.Round(CustoUsd / LimiteUsd.Value * 100))
            : 0;
    }

    public class AiUsageFuncRow
    {
        public string Funcionalidade { get; set; } = "";
        public long Chamadas { get; set; }
        public decimal CustoUsd { get; set; }
        public long Tokens { get; set; }
    }

    public class AiUsageModeloRow
    {
        public string Modelo { get; set; } = "";
        public long Chamadas { get; set; }
        public decimal CustoUsd { get; set; }
    }

    // ── Detalhe de IA por Agência ──────────────────────────────────────────────
    public class AiUsageAgenciaDetalheViewModel
    {
        public Guid AgenciaId { get; set; }
        public string NomeAgencia { get; set; } = "";
        public int Ano { get; set; }
        public int Mes { get; set; }
        public string MesLabel => new DateTime(Ano, Mes, 1).ToString("MMMM yyyy");

        // Cards resumo
        public decimal CustoMesUsd { get; set; }
        public long ChamadasMes { get; set; }
        public long TokensMes { get; set; }
        public int ChamadasBloqueadas { get; set; }

        // Limite atual
        public AiLimiteFormViewModel Limite { get; set; } = new();

        // Por funcionalidade
        public List<AiUsageFuncRow> PorFuncionalidade { get; set; } = new();

        // Histórico paginado
        public List<AiUsageRecordRow> Historico { get; set; } = new();
        public int TotalHistorico { get; set; }
        public int PaginaAtual { get; set; } = 1;
        public int TotalPaginas => (int)Math.Ceiling(TotalHistorico / (double)PageSize);
        public const int PageSize = 20;
    }

    public class AiUsageRecordRow
    {
        public DateTime DataHora { get; set; }
        public string Funcionalidade { get; set; } = "";
        public string Modelo { get; set; } = "";
        public int TotalTokens { get; set; }
        public decimal CustoUsd { get; set; }
        public bool Sucesso { get; set; }
        public string Status { get; set; } = "";
        public int DuracaoMs { get; set; }
    }

    // ── Formulário de limite ───────────────────────────────────────────────────
    public class AiLimiteFormViewModel
    {
        public Guid AgenciaId { get; set; }
        public decimal? LimiteMensalCusto { get; set; }
        public AiModoControle ModoControle { get; set; } = AiModoControle.Monitoramento;
        public int PercentualAlerta { get; set; } = 80;
        public bool PermitirExcedente { get; set; }
        public decimal? ValorExcedentePermitido { get; set; }
        public string? Motivo { get; set; }
    }
}
