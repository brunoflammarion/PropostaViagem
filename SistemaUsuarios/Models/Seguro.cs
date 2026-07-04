using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaUsuarios.Models
{
    public class Seguro
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PropostaId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; }

        [MaxLength(4000)]
        public string? Descricao { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Valor { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Proposta Proposta { get; set; }
        public virtual ICollection<SeguroImagem> Imagens { get; set; } = new List<SeguroImagem>();
        public virtual ICollection<SeguroDocumento> Documentos { get; set; } = new List<SeguroDocumento>();
    }
}
