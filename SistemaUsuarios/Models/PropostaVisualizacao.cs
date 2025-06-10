using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaUsuarios.Models
{
    public class PropostaVisualizacao
    {
        public Guid Id { get; set; }

        // Chave estrangeira (OBRIGATÓRIO)
        public Guid PropostaId { get; set; }
        public virtual Proposta? Proposta { get; set; }

        // Identificação da sessão (OBRIGATÓRIO)
        [Required]
        [StringLength(100)]
        public string SessionToken { get; set; } = string.Empty;

        // Dados de tempo (OBRIGATÓRIOS)
        public DateTime DataHoraInicio { get; set; }
        public DateTime? DataHoraFim { get; set; }
        public int TempoVisualizacaoSegundos { get; set; } = 0;

        // Dados do dispositivo (OPCIONAIS)
        [StringLength(50)]
        public string? TipoDispositivo { get; set; }

        [StringLength(200)]
        public string? Navegador { get; set; }

        [StringLength(200)]
        public string? SistemaOperacional { get; set; }

        [StringLength(50)]
        public string? ResolucaoTela { get; set; }

        [StringLength(10)]
        public string? IdiomaNavegador { get; set; }

        [StringLength(1000)]
        public string? UserAgent { get; set; }

        [StringLength(100)]
        public string? DeviceFingerprint { get; set; }

        // Dados de referência (OPCIONAIS)
        [StringLength(500)]
        public string? UrlReferenciador { get; set; }

        [StringLength(200)]
        public string? TipoReferenciador { get; set; }

        // Dados de localização (OPCIONAIS)
        [StringLength(100)]
        public string? EnderecoIP { get; set; }

        [StringLength(100)]
        public string? Pais { get; set; }

        [StringLength(100)]
        public string? Estado { get; set; }

        [StringLength(100)]
        public string? Cidade { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        // Dados de interação (OPCIONAIS com valores padrão)
        public int ScrollMaximoPercentual { get; set; } = 0;
        public int NumeroCliques { get; set; } = 0;
        public bool ClicouEmail { get; set; } = false;
        public bool ClicouWhatsApp { get; set; } = false;

        // Dados adicionais em JSON (OPCIONAL)
        [StringLength(2000)]
        public string? DadosAdicionais { get; set; }

        // Metadados (OBRIGATÓRIO)
        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }
}