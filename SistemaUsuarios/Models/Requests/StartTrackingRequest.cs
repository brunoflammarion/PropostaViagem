using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.Requests
{
    public class StartTrackingRequest
    {
        // Campos obrigatórios
        [Required]
        public Guid PropostaId { get; set; }

        [Required]
        public string SessionToken { get; set; } = string.Empty;

        [Required]
        public string DataHoraInicio { get; set; } = string.Empty;

        // Campos opcionais do dispositivo
        public string? TipoDispositivo { get; set; }
        public string? Navegador { get; set; }
        public string? SistemaOperacional { get; set; }
        public string? ResolucaoTela { get; set; }
        public string? IdiomaNavegador { get; set; }
        public string? UserAgent { get; set; }
        public string? DeviceFingerprint { get; set; }

        // Campos opcionais de referência
        public string? UrlReferenciador { get; set; }
        public string? TipoReferenciador { get; set; }

        // Campos opcionais de localização
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}