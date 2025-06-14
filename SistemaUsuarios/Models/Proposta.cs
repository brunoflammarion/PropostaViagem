using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Proposta
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Título é obrigatório")]
        [StringLength(500, ErrorMessage = "Título deve ter no máximo 500 caracteres")]
        public string Titulo { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public DateTime? DataModificacao { get; set; }

        [Required(ErrorMessage = "Usuário é obrigatório")]
        public Guid UsuarioId { get; set; }

        [Display(Name = "Data de Início")]
        public DateTime? DataInicio { get; set; }

        [Display(Name = "Data de Fim")]
        public DateTime? DataFim { get; set; }

        [Required(ErrorMessage = "Número de passageiros é obrigatório")]
        [Range(1, 50, ErrorMessage = "Número de passageiros deve estar entre 1 e 50")]
        [Display(Name = "Número de Passageiros")]
        public int NumeroPassageiros { get; set; } = 1;

        [Range(0, 20, ErrorMessage = "Número de crianças deve estar entre 0 e 20")]
        [Display(Name = "Número de Crianças")]
        public int NumeroCriancas { get; set; } = 0;

        [StringLength(1000, ErrorMessage = "URL da foto deve ter no máximo 1000 caracteres")]
        [Display(Name = "Foto de Capa")]
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

        // Navigation Properties
        public virtual Usuario Usuario { get; set; }
        public virtual Layout Layout { get; set; }

        // ✅ ADICIONANDO A PROPRIEDADE QUE ESTAVA FALTANDO
        public virtual ICollection<PropostaVisualizacao> PropostaVisualizacoes { get; set; } = new List<PropostaVisualizacao>();
    }

    public enum StatusProposta
    {
        Rascunho = 1,
        Enviada = 2,
        Aprovada = 3,
        Rejeitada = 4,
        Cancelada = 5
    }
}