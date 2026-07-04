using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaUsuarios.Models
{
    public class Transporte
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid DestinoId { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        [MaxLength(4000)]
        [Display(Name = "Descrição")]
        public string? Descricao { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Valor (R$)")]
        public decimal? Valor { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Destino Destino { get; set; } = null!;
        public virtual ICollection<TransporteImagem> Imagens { get; set; } = new List<TransporteImagem>();
        public virtual ICollection<TransporteDocumento> Documentos { get; set; } = new List<TransporteDocumento>();
    }
}
