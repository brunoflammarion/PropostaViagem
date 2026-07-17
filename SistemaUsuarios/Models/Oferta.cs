using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    /// <summary>
    /// Peça promocional criada pelo agente ou pela agência.
    /// Módulo independente de Proposta — foco em divulgação comercial.
    /// </summary>
    public class Oferta
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // ── Governança (espelha padrão de Proposta) ──────────────────────────────
        /// <summary>Usuário que criou a oferta (imutável).</summary>
        public Guid UsuarioId { get; set; }

        /// <summary>
        /// Master da equipe. Para usuários Master = UsuarioId.
        /// Para Associados = UsuarioMasterId do Associado.
        /// Permite que o master veja todas as ofertas da equipe.
        /// </summary>
        public Guid? UsuarioMasterId { get; set; }

        // ── Metadados ─────────────────────────────────────────────────────────────
        [Required]
        [MaxLength(200)]
        [Display(Name = "Nome da oferta")]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(50)]
        [Display(Name = "Template")]
        public string TemplateId { get; set; } = "destino";

        // ── Variáveis visuais ─────────────────────────────────────────────────────
        [MaxLength(20)]
        [Display(Name = "Cor 1")]
        public string? Cor1 { get; set; }

        [MaxLength(20)]
        [Display(Name = "Cor 2")]
        public string? Cor2 { get; set; }

        [MaxLength(20)]
        [Display(Name = "Cor 3")]
        public string? Cor3 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Imagem principal")]
        public string? ImagemPrincipalPath { get; set; }

        [MaxLength(500)]
        [Display(Name = "Logo")]
        public string? LogoPath { get; set; }

        /// <summary>
        /// JSON com overrides de slot por template. Estrutura para evolução futura:
        /// { "destino": { "titulo": "destino", "subtitulo": "periodoViagem" } }
        /// </summary>
        [MaxLength(2000)]
        public string? SlotsJson { get; set; }

        // ── Bloco principal ───────────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Título principal")]
        public string? TituloPrincipal { get; set; }

        [MaxLength(200)]
        [Display(Name = "Subtítulo")]
        public string? Subtitulo { get; set; }

        [MaxLength(500)]
        [Display(Name = "Descrição curta")]
        public string? DescricaoCurta { get; set; }

        [MaxLength(500)]
        [Display(Name = "Texto complementar")]
        public string? TextoComplementar { get; set; }

        [MaxLength(300)]
        [Display(Name = "Rodapé")]
        public string? Rodape { get; set; }

        [MaxLength(100)]
        [Display(Name = "CTA / chamada para ação")]
        public string? Cta { get; set; }

        // ── Bloco comercial ───────────────────────────────────────────────────────
        [MaxLength(100)]
        [Display(Name = "Preço")]
        public string? Preco { get; set; }

        [MaxLength(100)]
        [Display(Name = "Preço anterior")]
        public string? PrecoAnterior { get; set; }

        [MaxLength(100)]
        [Display(Name = "Texto \"a partir de\"")]
        public string? TextoAPartirDe { get; set; }

        [MaxLength(200)]
        [Display(Name = "Condição especial")]
        public string? CondicaoEspecial { get; set; }

        [MaxLength(200)]
        [Display(Name = "Parcelamento")]
        public string? Parcelamento { get; set; }

        [MaxLength(200)]
        [Display(Name = "Texto de urgência")]
        public string? TextoUrgencia { get; set; }

        [MaxLength(200)]
        [Display(Name = "Validade da oferta")]
        public string? ValidadeOferta { get; set; }

        // ── Bloco do produto/oferta ───────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Destino")]
        public string? Destino { get; set; }

        [MaxLength(200)]
        [Display(Name = "Origem")]
        public string? Origem { get; set; }

        [MaxLength(200)]
        [Display(Name = "Período da viagem")]
        public string? PeriodoViagem { get; set; }

        [MaxLength(50)]
        [Display(Name = "Qtd. noites")]
        public string? QtdNoites { get; set; }

        [MaxLength(200)]
        [Display(Name = "Companhia aérea")]
        public string? CompanhiaAerea { get; set; }

        [MaxLength(200)]
        [Display(Name = "Hotel / categoria")]
        public string? Hotel { get; set; }

        [MaxLength(200)]
        [Display(Name = "Regime / tipo de hospedagem")]
        public string? RegimeHospedagem { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Inclusões da oferta")]
        public string? InclusoesOferta { get; set; }

        [MaxLength(500)]
        [Display(Name = "Observações curtas")]
        public string? ObservacoesCurtas { get; set; }

        [MaxLength(500)]
        [Display(Name = "Regras / condições resumidas")]
        public string? RegrasCondicoes { get; set; }

        // ── Bloco de contato ──────────────────────────────────────────────────────
        [MaxLength(30)]
        [Display(Name = "WhatsApp")]
        public string? WhatsApp { get; set; }

        [MaxLength(30)]
        [Display(Name = "Telefone")]
        public string? Telefone { get; set; }

        [MaxLength(200)]
        [Display(Name = "E-mail")]
        public string? Email { get; set; }

        [MaxLength(100)]
        [Display(Name = "Instagram / rede social")]
        public string? Instagram { get; set; }

        [MaxLength(200)]
        [Display(Name = "Site")]
        public string? Site { get; set; }

        // ── Bloco institucional ───────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Nome da agência")]
        public string? NomeAgencia { get; set; }

        [MaxLength(200)]
        [Display(Name = "Selo promocional")]
        public string? SeloPromocional { get; set; }

        [MaxLength(100)]
        [Display(Name = "Tag promocional")]
        public string? TagPromocional { get; set; }

        [MaxLength(300)]
        [Display(Name = "Texto institucional curto")]
        public string? TextoInstitucional { get; set; }

        // ── Datas ─────────────────────────────────────────────────────────────────
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataModificacao { get; set; }

        // ── Demonstração ──────────────────────────────────────────────────────
        /// <summary>Quando true, esta oferta é uma cópia de demonstração criada automaticamente no onboarding.</summary>
        public bool IsConteudoDemonstracao { get; set; } = false;

        /// <summary>Id do ConteudoDemonstracao que originou esta cópia. Apenas auditoria — sem FK rígida.</summary>
        public Guid? ConteudoDemonstracaoOrigemId { get; set; }

        // ── Navigation Properties ─────────────────────────────────────────────────
        public virtual Usuario Usuario { get; set; } = null!;
        public virtual Usuario? UsuarioMaster { get; set; }
    }
}
