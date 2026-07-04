using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class ConfiguracaoLembrete
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UsuarioId { get; set; }

        [Required]
        [MaxLength(80)]
        public string Tipo { get; set; } = "";

        [Required]
        [MaxLength(80)]
        public string TemplateCodigo { get; set; } = "";

        public bool Habilitado { get; set; } = true;

        /// <summary>Dias relativos à referência. Negativo = antes, positivo = depois.</summary>
        public int? OffsetDias { get; set; }

        /// <summary>ANTES_INICIO | APOS_FIM | DIA_EVENTO</summary>
        [MaxLength(80)]
        public string? MomentoReferencia { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataAtualizacao { get; set; }

        public virtual Usuario Usuario { get; set; } = null!;
    }

    public static class MomentoReferenciaLembrete
    {
        public const string AntesInicio = "ANTES_INICIO";
        public const string AposFim     = "APOS_FIM";
        public const string DiaEvento   = "DIA_EVENTO";
    }
}
