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

        // Foto e identidade visual
        public string? FotoPath { get; set; }
        public string? LogoAgenciaPath { get; set; }
        public string? CorPrimaria { get; set; }
        public string? CorSecundaria { get; set; }
        public string? CorDestaque { get; set; }

        // Preferências de UI
        /// <summary>"lista" ou "kanban" — persiste a visão preferida do usuário na tela de propostas.</summary>
        [MaxLength(20)]
        public string PreferenciaVisualizacao { get; set; } = "lista";

        // ── Agência ──────────────────────────────────────────────────────────────
        [StringLength(150)]
        public string? NomeAgencia { get; set; }

        [StringLength(100)]
        public string? SlugAgencia { get; set; }

        // ── Hierarquia Master / Associado ────────────────────────────────────────
        /// <summary>Master cria associados e vê todas as propostas do grupo. Associado vê apenas as próprias.</summary>
        public TipoUsuario TipoUsuario { get; set; } = TipoUsuario.Master;

        /// <summary>Nulo para usuários Master; aponta para o Master responsável para usuários Associados.</summary>
        public Guid? UsuarioMasterId { get; set; }

        [MaxLength(64)]
        public string? CalendarioToken { get; set; }

        // Navigation properties de hierarquia
        public virtual Usuario? UsuarioMaster { get; set; }
        public virtual ICollection<Usuario> Associados { get; set; } = new List<Usuario>();
    }

    public enum TipoUsuario
    {
        Master    = 1,
        Associado = 2
    }

    public enum StatusUsuario
    {
        Novo = 1,
        Ativo = 2,
        Inativo = 3,
        Bloqueado = 4
    }
}