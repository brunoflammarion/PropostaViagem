using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Usuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [StringLength(150)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Telefone é obrigatório")]
        [StringLength(11, MinimumLength = 10, ErrorMessage = "Telefone deve ter entre 10 e 11 dígitos")]
        public string Telefone { get; set; } // Armazenado sem formatação: apenas números

        [Required(ErrorMessage = "CPF é obrigatório")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 dígitos")]
        public string CPF { get; set; } // Armazenado sem formatação: apenas números

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter no mínimo 6 caracteres")]
        public string Senha { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public StatusUsuario Status { get; set; } = StatusUsuario.Novo;
    }

    public enum StatusUsuario
    {
        Novo = 1,
        Ativo = 2,
        Inativo = 3,
        Bloqueado = 4
    }
}