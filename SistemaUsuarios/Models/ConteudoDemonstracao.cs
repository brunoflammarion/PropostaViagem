namespace SistemaUsuarios.Models
{
    public enum TipoConteudoDemonstracao
    {
        Proposta = 1,
        Oferta   = 2
    }

    public enum StatusAplicacaoDemonstracao
    {
        Sucesso = 1,
        Falha   = 2,
        Parcial = 3
    }

    /// <summary>
    /// Catálogo de propostas/ofertas usadas como modelos de demonstração para novas agências.
    /// EntidadeOrigemId não tem FK rígida — a entidade pode ser deletada sem cascata.
    /// </summary>
    public class ConteudoDemonstracao
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public TipoConteudoDemonstracao TipoConteudo { get; set; }

        /// <summary>Id da Proposta ou Oferta original usada como molde.</summary>
        public Guid EntidadeOrigemId { get; set; }

        /// <summary>Rótulo interno visível apenas no Platform Admin.</summary>
        public string? NomeAdministrativo { get; set; }

        public bool Ativo { get; set; } = true;

        /// <summary>Quando true, aplicar automaticamente no onboarding de novas agências.</summary>
        public bool AplicarAutomaticamente { get; set; } = true;

        public int Ordem { get; set; } = 0;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataAtualizacao { get; set; }

        public Guid CriadoPorAdminId { get; set; }
        public Guid? AtualizadoPorAdminId { get; set; }

        public virtual AdminPlataforma CriadoPorAdmin { get; set; } = null!;
        public virtual AdminPlataforma? AtualizadoPorAdmin { get; set; }

        public virtual ICollection<ConteudoDemonstracaoAplicado> Aplicacoes { get; set; } = new List<ConteudoDemonstracaoAplicado>();
    }

    /// <summary>
    /// Registro de cada aplicação de demonstração por agência — garante idempotência
    /// e permite auditoria de falhas. AgenciaMasterId não tem FK rígida.
    /// </summary>
    public class ConteudoDemonstracaoAplicado
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConteudoDemonstracaoId { get; set; }
        public virtual ConteudoDemonstracao ConteudoDemonstracao { get; set; } = null!;

        /// <summary>UsuarioMasterId da agência que recebeu a cópia.</summary>
        public Guid AgenciaMasterId { get; set; }

        /// <summary>Id do registro clonado (Proposta.Id ou Oferta.Id). Null se falhou antes de criar.</summary>
        public Guid? EntidadeClonadaId { get; set; }

        public DateTime DataAplicacao { get; set; } = DateTime.Now;

        public StatusAplicacaoDemonstracao StatusAplicacao { get; set; } = StatusAplicacaoDemonstracao.Sucesso;

        public string? MensagemErro { get; set; }
    }
}
