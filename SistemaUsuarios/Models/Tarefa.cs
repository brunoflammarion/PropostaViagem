using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class Tarefa
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UsuarioId { get; set; }

        public Guid? ClienteId { get; set; }

        public Guid? PropostaId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; } = "";

        public string? Descricao { get; set; }

        public DateTime DataVencimento { get; set; }

        [MaxLength(80)]
        public string Tipo { get; set; } = TarefaTipo.Geral;

        [MaxLength(20)]
        public string? Prioridade { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = TarefaStatus.Pendente;

        [MaxLength(30)]
        public string Origem { get; set; } = TarefaOrigem.Manual;

        public bool GeradaAutomaticamente { get; set; } = false;

        [MaxLength(80)]
        public string? TemplateCodigo { get; set; }

        public DateTime? DataConclusao { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataAtualizacao { get; set; }

        public bool IsDeleted { get; set; } = false;

        public virtual Usuario Usuario { get; set; } = null!;
        public virtual Cliente? Cliente { get; set; }
        public virtual Proposta? Proposta { get; set; }
    }

    public static class TarefaStatus
    {
        public const string Pendente  = "Pendente";
        public const string Concluida = "Concluida";
        public const string Cancelada = "Cancelada";
    }

    public static class TarefaOrigem
    {
        public const string Manual     = "Manual";
        public const string Automatica = "Automatica";
    }

    public static class TarefaPrioridade
    {
        public const string Alta  = "Alta";
        public const string Media = "Media";
        public const string Baixa = "Baixa";
    }

    public static class TarefaTipo
    {
        public const string Followup    = "Followup";
        public const string Operacional = "Operacional";
        public const string Comercial   = "Comercial";
        public const string Aniversario = "Aniversario";
        public const string Geral       = "Geral";
    }
}
