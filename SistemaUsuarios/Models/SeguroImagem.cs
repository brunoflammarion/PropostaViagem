namespace SistemaUsuarios.Models
{
    public class SeguroImagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SeguroId { get; set; }
        public string CaminhoImagem { get; set; }
        public string? Descricao { get; set; }
        public int Ordem { get; set; } = 1;
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public virtual Seguro Seguro { get; set; }
    }
}
