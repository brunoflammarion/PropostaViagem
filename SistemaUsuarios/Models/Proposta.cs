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

        /// <summary>Master ao qual esta proposta pertence. Para usuários Master, igual ao UsuarioId.
        /// Para Associados, aponta para o Master que gerencia o grupo.</summary>
        public Guid? UsuarioMasterId { get; set; }

        /// <summary>
        /// Responsável atual pela proposta. Começa igual ao UsuarioId (criador),
        /// mas pode ser transferida pelo master para outro membro da equipe.
        /// UsuarioId nunca muda — preserva o histórico de autoria.
        /// </summary>
        public Guid? UsuarioResponsavelId { get; set; }

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
        public string? FotoCapa { get; set; } // ALTERADO: Agora nullable

        [Display(Name = "Layout")]
        public int? LayoutId { get; set; }

        [Display(Name = "Observações Gerais")]
        public string? ObservacoesGerais { get; set; } // ALTERADO: Agora nullable

        [Display(Name = "Status")]
        public StatusProposta StatusProposta { get; set; } = StatusProposta.Rascunho;

        [Display(Name = "Link Público Ativo")]
        public bool LinkPublicoAtivo { get; set; } = true;

        [Display(Name = "Data de Expiração do Link")]
        public DateTime? DataExpiracaoLink { get; set; }

        /// <summary>Código curto de acesso (ex: MAR-724). Null = sem proteção por código.</summary>
        [MaxLength(10)]
        public string? CodigoAcesso { get; set; }

        /// <summary>Resumo/fechamento da proposta escrito pelo agente (HTML do editor rico).</summary>
        public string? ResumoProposta { get; set; }

        /// <summary>HTML com condições gerais da proposta, editado pelo agente (editor rico na aba Condições).</summary>
        public string? CondicoesPropostaHtml { get; set; }

        /// <summary>HTML com valores e forma de pagamento da proposta, editado pelo agente (editor rico na aba Condições).</summary>
        public string? ValoresPropostaHtml { get; set; }

        // ── Configuração de avaliação pelo cliente ───────────────────────────
        /// <summary>Quando true e houver +1 hospedagem, exibe avaliação comparativa na proposta pública.</summary>
        public bool SolicitarAvaliacaoHospedagem { get; set; } = false;

        /// <summary>Quando true e houver +1 acomodação, exibe avaliação comparativa na proposta pública.</summary>
        public bool SolicitarAvaliacaoAcomodacao { get; set; } = false;

        /// <summary>Quando true e houver +1 experiência, exibe avaliação comparativa na proposta pública.</summary>
        public bool SolicitarAvaliacaoExperiencia { get; set; } = false;

        // Navigation Properties
        public virtual Usuario Usuario { get; set; }
        public virtual Usuario? UsuarioMaster { get; set; }
        public virtual Usuario? UsuarioResponsavel { get; set; }
        public virtual Layout Layout { get; set; }

        // ✅ RELACIONAMENTO COM DESTINOS (1:N)
        public virtual ICollection<Destino> Destinos { get; set; } = new List<Destino>();

        // ✅ RELACIONAMENTO COM VISUALIZAÇÕES (1:N)
        public virtual ICollection<PropostaVisualizacao> PropostaVisualizacoes { get; set; } = new List<PropostaVisualizacao>();

        // ✅ RELACIONAMENTO COM VOOS (1:N)
        public virtual ICollection<Voo> Voos { get; set; } = new List<Voo>();

        // ✅ RELACIONAMENTO COM SEGUROS (1:N)
        public virtual ICollection<Seguro> Seguros { get; set; } = new List<Seguro>();

        // ✅ RELACIONAMENTO COM CLIENTE PRINCIPAL (N:1 — cliente é entidade própria)
        public Guid? ClienteId { get; set; }
        public virtual Cliente? Cliente { get; set; }

        // ✅ RELACIONAMENTO COM PASSAGEIROS ADICIONAIS (1:N)
        public virtual ICollection<PassageiroProposta> PassageirosProposta { get; set; } = new List<PassageiroProposta>();
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