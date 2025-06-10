using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Layout
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(500)]
        public string Descricao { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }
}