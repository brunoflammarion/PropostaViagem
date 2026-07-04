using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class LeadCaptureSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UsuarioId { get; set; }

        public bool IsActive { get; set; } = false;

        [StringLength(200)]
        public string? WelcomeMessage { get; set; }

        [StringLength(80)]
        public string? ResponseTimeText { get; set; }

        // Campos opcionais configuráveis
        public bool ShowEmail { get; set; } = true;
        public bool ShowOriginCity { get; set; } = false;
        public bool ShowTravelDates { get; set; } = false;
        public bool ShowAdults { get; set; } = false;
        public bool ShowChildren { get; set; } = false;
        public bool ShowBudget { get; set; } = false;
        public bool ShowTripType { get; set; } = false;
        public bool ShowAccommodationPreference { get; set; } = false;
        public bool ShowNotes { get; set; } = false;
        public bool ShowBestContactTime { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual Usuario Usuario { get; set; } = null!;
    }
}
