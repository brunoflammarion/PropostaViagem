using System.ComponentModel.DataAnnotations;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace SistemaUsuarios.Models
{
    public class Destino
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // ✅ Mudança: Guid em vez de int

        [Required]
        public Guid PropostaId { get; set; } // ✅ FK para Proposta

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        [MaxLength(1000)]
        public string? Descricao { get; set; }

        public DateTime? DataChegada { get; set; }

        public DateTime? DataSaida { get; set; }

        public int Ordem { get; set; } = 1;

        [MaxLength(100)]
        public string? Pais { get; set; }

        [MaxLength(100)]
        public string? Cidade { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // ✅ NAVEGAÇÃO PARA PROPOSTA (N:1)
        public virtual Proposta Proposta { get; set; }

        // ✅ NAVEGAÇÃO PARA FOTOS (1:N)
        public virtual ICollection<DestinoFoto> Fotos { get; set; } = new List<DestinoFoto>();

        // NAVEGAÇÃO PARA HOSPEDAGENS (1:N)
        public virtual ICollection<Hospedagem> Hospedagens { get; set; } = new List<Hospedagem>();

        // NAVEGAÇÃO PARA EXPERIÊNCIAS (1:N)
        public virtual ICollection<Experiencia> Experiencias { get; set; } = new List<Experiencia>();

        // NAVEGAÇÃO PARA TRANSPORTES (1:N)
        public virtual ICollection<Transporte> Transportes { get; set; } = new List<Transporte>();

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public Point? Localizacao { get; set; }

        public string? DescricaoLLM { get; set; }
        public string? AtracoesLLM { get; set; } // Pode ser serializado como JSON
        public string? GastronomiaLLM { get; set; }
        public string? InformacoesPraticasLLM { get; set; }
        public string? MalaViagemLLM { get; set; }
        public string? CuidadosLLM { get; set; }
        public string? PratosTipicosLLM { get; set; }
    }
}