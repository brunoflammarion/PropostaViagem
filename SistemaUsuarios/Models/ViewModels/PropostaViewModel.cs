using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    public class PropostaViewModel
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

        // UPLOAD DE FOTO - SEM VALIDAÇÕES
        [Display(Name = "Foto de Capa")]
        public IFormFile FotoCapaUpload { get; set; }

        // CAMINHO DA FOTO SALVA - SEM VALIDAÇÕES
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
        [DataType(DataType.DateTime)]
        public DateTime? DataExpiracaoLink { get; set; }

        public DateTime? DataCriacao { get; set; }
        public DateTime? DataModificacao { get; set; }
    }

    public class PropostaListViewModel
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public int NumeroPassageiros { get; set; }
        public int NumeroCriancas { get; set; }
        public StatusProposta StatusProposta { get; set; }
        public string NomeUsuario { get; set; }
        public Guid UsuarioId { get; set; }
        public bool LinkPublicoAtivo { get; set; }
        public string FotoCapa { get; set; }
        public DateTime? DataModificacao { get; set; }

        // Para link de compartilhamento
        public string LinkCompartilhamento => $"/Proposta/Publico/{Id}";

        // Para exibir status com cor
        public string StatusCssClass => StatusProposta switch
        {
            StatusProposta.Rascunho => "bg-secondary",
            StatusProposta.Enviada => "bg-warning",
            StatusProposta.Aprovada => "bg-success",
            StatusProposta.Rejeitada => "bg-danger",
            StatusProposta.Cancelada => "bg-dark",
            _ => "bg-info"
        };

        // Para calcular duração da viagem
        public int? DuracaoDias
        {
            get
            {
                if (DataInicio.HasValue && DataFim.HasValue)
                    return (DataFim.Value - DataInicio.Value).Days + 1;
                return null;
            }
        }
    }
    public class PropostaFiltroViewModel
    {
        [Display(Name = "Buscar por título")]
        public string TermoBusca { get; set; }

        [Display(Name = "Status")]
        public StatusProposta? FiltroStatus { get; set; }

        [Display(Name = "Data de início")]
        [DataType(DataType.Date)]
        public DateTime? DataInicioFiltro { get; set; }

        [Display(Name = "Data de fim")]
        [DataType(DataType.Date)]
        public DateTime? DataFimFiltro { get; set; }

        [Display(Name = "Usuário")]
        public Guid? FiltroUsuario { get; set; }

        [Display(Name = "Link ativo")]
        public bool? FiltroLinkAtivo { get; set; }

        // Lista de propostas filtradas
        public List<PropostaListViewModel> Propostas { get; set; } = new();

        // Para dropdown de usuários
        public List<Usuario> Usuarios { get; set; } = new();

        // Para estatísticas
        public int TotalPropostas { get; set; }
        public int TotalRascunhos { get; set; }
        public int TotalEnviadas { get; set; }
        public int TotalAprovadas { get; set; }
    }
}