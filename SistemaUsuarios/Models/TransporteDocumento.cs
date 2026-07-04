using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class TransporteDocumento
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TransporteId { get; set; }

        [Required]
        [MaxLength(300)]
        public string NomeOriginal { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string CaminhoArquivo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string TipoArquivo { get; set; } = string.Empty;

        public long Tamanho { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Transporte Transporte { get; set; } = null!;
    }
}
