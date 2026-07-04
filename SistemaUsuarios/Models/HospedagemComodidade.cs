using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class HospedagemComodidade
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospedagemId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public virtual Hospedagem Hospedagem { get; set; }
    }
}
