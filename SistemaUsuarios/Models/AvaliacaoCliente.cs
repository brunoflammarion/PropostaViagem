using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaUsuarios.Models
{
    public enum TipoItemAvaliacao
    {
        Hospedagem  = 1,
        Acomodacao  = 2,
        Experiencia = 3,
    }

    public class AvaliacaoCliente
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PropostaId { get; set; }

        [Required]
        public TipoItemAvaliacao TipoItem { get; set; }

        /// <summary>
        /// ID da entidade avaliada: Hospedagem.Id, Acomodacao.Id ou Experiencia.Id.
        /// </summary>
        [Required]
        public Guid ItemId { get; set; }

        /// <summary>Nota de 1 a 5 atribuída pelo cliente.</summary>
        [Required]
        [Range(1, 5)]
        public int Nota { get; set; }

        /// <summary>Comentário opcional do cliente sobre o item.</summary>
        [MaxLength(2000)]
        public string? Comentario { get; set; }

        /// <summary>true quando o cliente marcou este item como favorito/preferido.</summary>
        public bool Favorito { get; set; } = false;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation
        public virtual Proposta Proposta { get; set; } = null!;
    }
}
