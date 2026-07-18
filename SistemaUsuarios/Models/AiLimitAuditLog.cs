namespace SistemaUsuarios.Models
{
    public class AiLimitAuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AgenciaId { get; set; }
        public Guid AdministradorId { get; set; }
        public string Campo { get; set; } = "";
        public string? ValorAnterior { get; set; }
        public string? ValorNovo { get; set; }
        public string? Motivo { get; set; }
        public DateTime DataHora { get; set; } = DateTime.UtcNow;
    }
}
