namespace SistemaUsuarios.Models.ViewModels
{
    public class LeadDetalheViewModel
    {
        public Lead Lead { get; set; } = null!;
        public List<LeadHistorico> Historico { get; set; } = new();
    }

    public class LeadEditarViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string WhatsApp { get; set; } = "";
        public string Destination { get; set; } = "";
        public string? Email { get; set; }
        public string? OriginCity { get; set; }
        public string? TravelDates { get; set; }
        public int? Adults { get; set; }
        public int? Children { get; set; }
        public string? Budget { get; set; }
        public string? TripType { get; set; }
        public string? AccommodationPreference { get; set; }
        public string? Notes { get; set; }
        public string? BestContactTime { get; set; }
        public LeadStatus Status { get; set; }
    }
}
