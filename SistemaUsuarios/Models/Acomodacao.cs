using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Acomodacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospedagemId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        [MaxLength(4000)]
        public string? Descricao { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navegação para Hospedagem (N:1)
        public virtual Hospedagem Hospedagem { get; set; }

        // Navegação para Fotos da Acomodação (1:N)
        public virtual ICollection<AcomodacaoFoto> Fotos { get; set; } = new List<AcomodacaoFoto>();

        // Navegação para Comodidades da Acomodação (1:N)
        public virtual ICollection<AcomodacaoComodidade> Comodidades { get; set; } = new List<AcomodacaoComodidade>();
    }
}
