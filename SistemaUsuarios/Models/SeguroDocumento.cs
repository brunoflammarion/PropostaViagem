namespace SistemaUsuarios.Models
{
    public class SeguroDocumento
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SeguroId { get; set; }
        public string NomeOriginal { get; set; }
        public string CaminhoArquivo { get; set; }
        public string TipoArquivo { get; set; }
        public long Tamanho { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public virtual Seguro Seguro { get; set; }
    }
}
