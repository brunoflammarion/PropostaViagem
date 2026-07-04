using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class VooAnexo
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid VooId { get; set; }

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
        public virtual Voo Voo { get; set; } = null!;
    }
}
