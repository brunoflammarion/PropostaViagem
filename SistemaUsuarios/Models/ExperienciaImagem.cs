using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class ExperienciaImagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ExperienciaId { get; set; }

        [Required]
        [MaxLength(500)]
        public string CaminhoImagem { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Descricao { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Experiencia Experiencia { get; set; } = null!;
    }
}
