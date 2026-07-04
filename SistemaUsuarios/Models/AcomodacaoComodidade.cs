using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class AcomodacaoComodidade
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid AcomodacaoId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navegação para Acomodacao (N:1)
        public virtual Acomodacao Acomodacao { get; set; }
    }
}
