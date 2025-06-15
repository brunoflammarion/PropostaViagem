using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    // ViewModel para estatísticas da landing page
    public class LandingStatisticsViewModel
    {
        public int TotalAgentes { get; set; }
        public int TotalPropostas { get; set; }
        public int TotalVisualizacoes { get; set; }
        public double MediaVisualizacoesPorProposta { get; set; }
        public double TaxaConversao { get; set; }
        public decimal FaturamentoMedio { get; set; }
        public double AvaliacaoMedia { get; set; }
    }

    // ViewModel para o formulário de cadastro inicial da landing
    public class LandingCadastroViewModel
    {
        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 100 caracteres")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [StringLength(150)]
        public string Email { get; set; }

        [Required(ErrorMessage = "WhatsApp é obrigatório")]
        [StringLength(20, MinimumLength = 10, ErrorMessage = "Telefone deve ter entre 10 e 15 dígitos")]
        public string Telefone { get; set; }

        [Required(ErrorMessage = "Selecione como você trabalha")]
        public string TipoAgente { get; set; }

        // Campos opcionais para segmentação
        public string UrlReferenciador { get; set; }
        public string CampanhaOrigem { get; set; }
    }

    // ViewModel para completar o cadastro
    public class CompletarCadastroViewModel : IValidatableObject
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 100 caracteres")]
        [Display(Name = "Nome Completo")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "WhatsApp é obrigatório")]
        [Display(Name = "WhatsApp")]
        public string Telefone { get; set; }

        [Required(ErrorMessage = "CPF é obrigatório")]
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

        [Display(Name = "Aceito os termos de uso")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Você deve aceitar os termos de uso")]
        public bool AceitaTermos { get; set; }

        [Display(Name = "Quero receber emails com dicas e novidades")]
        public bool AceitaEmails { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validação personalizada de CPF
            if (!string.IsNullOrEmpty(CPF) && !ValidarCPF(CPF))
            {
                results.Add(new ValidationResult("CPF inválido", new[] { nameof(CPF) }));
            }

            // Validação de telefone (formato brasileiro)
            if (!string.IsNullOrEmpty(Telefone))
            {
                var telefoneNumeros = System.Text.RegularExpressions.Regex.Replace(Telefone, @"[^\d]", "");
                if (telefoneNumeros.Length < 10 || telefoneNumeros.Length > 11)
                {
                    results.Add(new ValidationResult("Telefone deve ter 10 ou 11 dígitos", new[] { nameof(Telefone) }));
                }
            }

            return results;
        }

        private bool ValidarCPF(string cpf)
        {
            // Remove formatação
            cpf = System.Text.RegularExpressions.Regex.Replace(cpf, @"[^\d]", "");

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

    // ViewModel para analytics da landing page
    public class LandingAnalyticsViewModel
    {
        public DateTime Data { get; set; }
        public int Visualizacoes { get; set; }
        public int Cadastros { get; set; }
        public double TaxaConversao { get; set; }
        public string OrigemTrafico { get; set; }
        public Dictionary<string, int> TiposAgente { get; set; } = new();
        public Dictionary<string, int> Dispositivos { get; set; } = new();
    }

    // ViewModel para campanhas de marketing
    public class CampanhaMarketingViewModel
    {
        public string NomeCampanha { get; set; }
        public string UrlOrigem { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public int Visualizacoes { get; set; }
        public int Cadastros { get; set; }
        public double TaxaConversao { get; set; }
        public decimal CustoTotal { get; set; }
        public decimal CustoPorCadastro { get; set; }
        public bool Ativa { get; set; }
    }
}