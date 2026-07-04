using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public enum Genero
    {
        Masculino = 1,
        Feminino = 2,
        Outro = 3
    }

    public class Cliente
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UsuarioId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        public string? FotoPath { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(30)]
        public string? Telefone { get; set; }

        public DateTime? DataNascimento { get; set; }

        public Genero? Genero { get; set; }

        [MaxLength(20)]
        public string? Cpf { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public DateTime? DataEntradaCliente { get; set; }

        // Endereço
        [MaxLength(20)]
        public string? Cep { get; set; }

        [MaxLength(255)]
        public string? Logradouro { get; set; }

        [MaxLength(120)]
        public string? Cidade { get; set; }

        [MaxLength(2)]
        public string? Estado { get; set; }

        // Indicação
        public Guid? ClienteIndicadorId { get; set; }

        // Exclusão lógica
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedByUserId { get; set; }

        // Navigation
        public virtual Usuario Usuario { get; set; }
        public virtual Cliente? ClienteIndicador { get; set; }
        public virtual ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();

        // Computed
        public int? IdadeCalculada => DataNascimento.HasValue
            ? CalcularIdade(DataNascimento.Value)
            : null;

        private static int CalcularIdade(DateTime nascimento)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - nascimento.Year;
            if (nascimento.Date > hoje.AddYears(-idade)) idade--;
            return idade;
        }
    }
}
