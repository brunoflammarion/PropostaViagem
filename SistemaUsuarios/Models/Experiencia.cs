using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaUsuarios.Models
{
    public class Experiencia
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid DestinoId { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Tipo de Passeio")]
        public string TipoPasseio { get; set; } = string.Empty;

        [MaxLength(4000)]
        [Display(Name = "Descrição")]
        public string? Descricao { get; set; }

        /// <summary>URL de vídeo (YouTube, Vimeo, etc.) para incorporar na experiência.</summary>
        [MaxLength(500)]
        [Display(Name = "URL do Vídeo")]
        public string? VideoUrl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Valor (R$)")]
        public decimal? Valor { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Destino Destino { get; set; } = null!;
        public virtual ICollection<ExperienciaImagem> Imagens { get; set; } = new List<ExperienciaImagem>();
        public virtual ICollection<ExperienciaArquivo> Arquivos { get; set; } = new List<ExperienciaArquivo>();
    }
}
