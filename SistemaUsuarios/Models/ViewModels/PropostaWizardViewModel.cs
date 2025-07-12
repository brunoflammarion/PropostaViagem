using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    public class PropostaWizardViewModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Título é obrigatório")]
        [StringLength(500, ErrorMessage = "Título deve ter no máximo 500 caracteres")]
        [Display(Name = "Título da Proposta")]
        public string Titulo { get; set; }

        public Guid? UsuarioId { get; set; }

        [Display(Name = "Data de Início")]
        [DataType(DataType.Date)]
        public DateTime? DataInicio { get; set; }

        [Display(Name = "Data de Fim")]
        [DataType(DataType.Date)]
        public DateTime? DataFim { get; set; }

        [Required(ErrorMessage = "Número de passageiros é obrigatório")]
        [Range(1, 50, ErrorMessage = "Número de passageiros deve estar entre 1 e 50")]
        [Display(Name = "Número de Passageiros")]
        public int NumeroPassageiros { get; set; } = 1;

        [Range(0, 20, ErrorMessage = "Número de crianças deve estar entre 0 e 20")]
        [Display(Name = "Número de Crianças")]
        public int NumeroCriancas { get; set; } = 0;

        [Display(Name = "Foto de Capa")]
        public IFormFile FotoCapaUpload { get; set; }

        public string FotoCapa { get; set; }

        [Display(Name = "Layout")]
        public int? LayoutId { get; set; }

        [Display(Name = "Observações Gerais")]
        public string ObservacoesGerais { get; set; }

        [Display(Name = "Status")]
        public StatusProposta StatusProposta { get; set; } = StatusProposta.Rascunho;

        [Display(Name = "Link Público Ativo")]
        public bool LinkPublicoAtivo { get; set; } = true;

        [Display(Name = "Data de Expiração do Link")]
        public DateTime? DataExpiracaoLink { get; set; }

        public DateTime? DataCriacao { get; set; }
        public DateTime? DataModificacao { get; set; }

        // Lista de destinos para a aba
        public List<DestinoViewModel> Destinos { get; set; } = new List<DestinoViewModel>();
    }
} 