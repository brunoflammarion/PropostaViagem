using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class PassageiroVoo
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid VooId { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Assento")]
        public string? Assento { get; set; }

        [Range(0, 10)]
        [Display(Name = "Bagagens Despachadas")]
        public int BagagensDespachadas { get; set; } = 0;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual Voo Voo { get; set; } = null!;
    }
}
