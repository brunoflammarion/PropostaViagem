using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Lead
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UsuarioId { get; set; }

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = "";

        [Required]
        [StringLength(20)]
        public string WhatsApp { get; set; } = "";

        [Required]
        [StringLength(300)]
        public string Destination { get; set; } = "";

        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(150)]
        public string? OriginCity { get; set; }

        [StringLength(100)]
        public string? TravelDates { get; set; }

        public int? Adults { get; set; }

        public int? Children { get; set; }

        [StringLength(100)]
        public string? Budget { get; set; }

        [StringLength(100)]
        public string? TripType { get; set; }

        [StringLength(150)]
        public string? AccommodationPreference { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? BestContactTime { get; set; }

        [StringLength(80)]
        public string Source { get; set; } = "Landing Page Pública";

        public LeadStatus Status { get; set; } = LeadStatus.Novo;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? ExcluidoEm { get; set; }

        public Guid? ExcluidoPorUsuarioId { get; set; }

        // Vínculos gerados ao preparar proposta
        public Guid? ClienteId { get; set; }
        public Guid? PropostaId { get; set; }

        public virtual Usuario Usuario { get; set; } = null!;
        public virtual Cliente? Cliente { get; set; }
        public virtual Proposta? Proposta { get; set; }
    }

    public enum LeadStatus
    {
        Novo = 1,
        Contatado = 2,
        EmNegociacao = 3,
        Convertido = 4,
        Perdido = 5
    }
}
