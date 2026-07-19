namespace SistemaUsuarios.Models
{
    public class ListaVipCadastro
    {
        public Guid     Id            { get; set; } = Guid.NewGuid();
        public string   Nome          { get; set; } = "";
        public string   Email         { get; set; } = "";
        public string   Whatsapp      { get; set; } = "";
        public string   NomeAgencia   { get; set; } = "";
        public string?  Cidade        { get; set; }
        public string?  Instagram     { get; set; }
        public string?  PropostasPorMes { get; set; }
        public string?  Origem        { get; set; }
        public string?  UtmSource     { get; set; }
        public string?  UtmMedium     { get; set; }
        public string?  UtmCampaign   { get; set; }
        public string?  PaginaOrigem  { get; set; }
        public string?  Referrer      { get; set; }
        public string?  Ip            { get; set; }
        public string?  UserAgent     { get; set; }
        public DateTime DataCadastro  { get; set; } = DateTime.Now;
        public bool     Visualizado   { get; set; } = false;
        public DateTime? DataVisualizacao { get; set; }
    }
}
