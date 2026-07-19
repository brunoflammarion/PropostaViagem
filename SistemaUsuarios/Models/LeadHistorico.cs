using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class LeadHistorico
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid LeadId { get; set; }

        public Guid AgenciaId { get; set; }

        public Guid UsuarioId { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoAcao { get; set; } = "";

        [StringLength(100)]
        public string? CampoAlterado { get; set; }

        [StringLength(500)]
        public string? ValorAnterior { get; set; }

        [StringLength(500)]
        public string? ValorNovo { get; set; }

        [StringLength(500)]
        public string? Observacao { get; set; }

        public DateTime DataHora { get; set; } = DateTime.Now;

        public virtual Lead Lead { get; set; } = null!;
    }
}
