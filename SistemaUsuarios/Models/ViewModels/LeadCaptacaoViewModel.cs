using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    public class LeadCaptacaoViewModel
    {
        public Guid SettingsId { get; set; }

        public string? NomeAgencia { get; set; }
        public string? SlugAgencia { get; set; }
        public string PublicUrl { get; set; } = "";

        // Status da captação
        public bool IsActive { get; set; }

        // Textos configuráveis
        [StringLength(200, ErrorMessage = "Mensagem deve ter no máximo 200 caracteres")]
        public string? WelcomeMessage { get; set; }

        [StringLength(80, ErrorMessage = "Tempo de resposta deve ter no máximo 80 caracteres")]
        public string? ResponseTimeText { get; set; }

        // Campos opcionais
        public bool ShowEmail { get; set; }
        public bool ShowOriginCity { get; set; }
        public bool ShowTravelDates { get; set; }
        public bool ShowAdults { get; set; }
        public bool ShowChildren { get; set; }
        public bool ShowBudget { get; set; }
        public bool ShowTripType { get; set; }
        public bool ShowAccommodationPreference { get; set; }
        public bool ShowNotes { get; set; }
        public bool ShowBestContactTime { get; set; }

        // Estatísticas
        public int TotalLeads { get; set; }
        public int LeadsNovos { get; set; }
    }
}
