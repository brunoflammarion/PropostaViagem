namespace SistemaUsuarios.Models
{
    public enum AiModoControle
    {
        Monitoramento = 1,
        Alerta = 2,
        Bloqueio = 3
    }

    public class AiAgencyLimit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AgenciaId { get; set; }

        public decimal? LimiteMensalCusto { get; set; }
        public string Moeda { get; set; } = "USD";

        public int? LimiteMensalChamadas { get; set; }
        public long? LimiteMensalTokens { get; set; }

        public AiModoControle ModoControle { get; set; } = AiModoControle.Monitoramento;
        public int PercentualAlerta { get; set; } = 80;
        public bool Ativo { get; set; } = true;

        public bool PermitirExcedente { get; set; } = false;
        public decimal? ValorExcedentePermitido { get; set; }

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
        public Guid? AtualizadoPorAdminId { get; set; }
    }
}
