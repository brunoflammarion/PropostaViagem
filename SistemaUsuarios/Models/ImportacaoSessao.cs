using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class ImportacaoSessao
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UsuarioId { get; set; }
        public Guid UsuarioMasterId { get; set; }

        // Proposta criada após confirmação — null enquanto o draft ainda está em revisão
        public Guid? PropostaId { get; set; }

        // TravelProposalDraft serializado como JSON (nvarchar(max))
        public string DraftJson { get; set; } = "{}";

        // Arquivos processados nesta sessão (nomes separados por |)
        [MaxLength(2000)]
        public string? SourceFiles { get; set; }

        public ImportacaoSessaoStatus Status { get; set; } = ImportacaoSessaoStatus.AguardandoConfirmacao;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
        public DateTime ExpiradoEm { get; set; } = DateTime.UtcNow.AddHours(24);
    }

    public enum ImportacaoSessaoStatus
    {
        AguardandoConfirmacao = 0,
        Concluida             = 1,
        Cancelada             = 2
    }
}
