using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Models.ViewModels
{
    public class UsuarioViewModel : IValidatableObject
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 100 caracteres")]
        [Display(Name = "Nome Completo")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Email deve ter um formato válido")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Telefone é obrigatório")]
        [RegularExpression(@"^\(\d{2}\)\s\d{4,5}-\d{4}$", ErrorMessage = "Telefone deve estar no formato (11) 99999-9999")]
        [Display(Name = "Telefone")]
        public string Telefone { get; set; }

        [Required(ErrorMessage = "CPF é obrigatório")]
        [RegularExpression(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", ErrorMessage = "CPF deve estar no formato 000.000.000-00")]
        [Display(Name = "CPF")]
        public string CPF { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter no mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; }

        [Compare("Senha", ErrorMessage = "Senhas não conferem")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Senha")]
        public string ConfirmarSenha { get; set; }

        public StatusUsuario Status { get; set; }

        public DateTime? DataCriacao { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validação personalizada de CPF
            if (!string.IsNullOrEmpty(CPF) && !ValidarCPF(CPF))
            {
                results.Add(new ValidationResult("CPF inválido", new[] { nameof(CPF) }));
            }

            // Validação de nome (não pode conter apenas números)
            if (!string.IsNullOrEmpty(Nome) && Regex.IsMatch(Nome, @"^\d+$"))
            {
                results.Add(new ValidationResult("Nome não pode conter apenas números", new[] { nameof(Nome) }));
            }

            return results;
        }

        private bool ValidarCPF(string cpf)
        {
            // Remove formatação
            cpf = cpf.Replace(".", "").Replace("-", "");

            if (cpf.Length != 11)
                return false;

            // Verifica se todos os dígitos são iguais
            if (cpf.All(c => c == cpf[0]))
                return false;

            // Validação do primeiro dígito verificador
            int soma = 0;
            for (int i = 0; i < 9; i++)
                soma += int.Parse(cpf[i].ToString()) * (10 - i);

            int resto = soma % 11;
            int digitoVerificador1 = resto < 2 ? 0 : 11 - resto;

            if (int.Parse(cpf[9].ToString()) != digitoVerificador1)
                return false;

            // Validação do segundo dígito verificador
            soma = 0;
            for (int i = 0; i < 10; i++)
                soma += int.Parse(cpf[i].ToString()) * (11 - i);

            resto = soma % 11;
            int digitoVerificador2 = resto < 2 ? 0 : 11 - resto;

            return int.Parse(cpf[10].ToString()) == digitoVerificador2;
        }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; }

        [Display(Name = "Lembrar-me")]
        public bool LembrarMe { get; set; }
    }
}