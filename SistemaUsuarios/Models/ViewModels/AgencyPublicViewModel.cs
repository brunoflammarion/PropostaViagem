namespace SistemaUsuarios.Models.ViewModels
{
    public class AgencyPublicViewModel
    {
        public string NomeAgencia { get; set; } = "";
        public string SlugAgencia { get; set; } = "";
        public string? FotoPath { get; set; }
        public string? CorPrimaria { get; set; }
        public string? CorSecundaria { get; set; }
        public string? CorDestaque { get; set; }

        public bool CaptacaoAtiva { get; set; }
        public string? WelcomeMessage { get; set; }
        public string? ResponseTimeText { get; set; }

        // Campos do formulário
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

        // Estado pós-envio
        public bool Enviado { get; set; }
        public string? ErroEnvio { get; set; }
    }
}
