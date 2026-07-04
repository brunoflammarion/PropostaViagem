using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class HospedagemFoto
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospedagemId { get; set; }

        [Required]
        [MaxLength(500)]
        public string CaminhoFoto { get; set; }

        [MaxLength(200)]
        public string? Descricao { get; set; }

        public int Ordem { get; set; } = 1;

        public bool Principal { get; set; } = false;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navegação para Hospedagem (N:1)
        public virtual Hospedagem Hospedagem { get; set; }
    }
}
