namespace SistemaUsuarios.Models
{
    public class AiModelPricing
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Provedor { get; set; } = "OpenAI";
        public string Modelo { get; set; } = "";
        public decimal PrecoInputPorMilhao { get; set; }
        public decimal PrecoCachedInputPorMilhao { get; set; }
        public decimal PrecoOutputPorMilhao { get; set; }
        public string Moeda { get; set; } = "USD";
        public DateTime VigenciaInicio { get; set; }
        public DateTime? VigenciaFim { get; set; }
        public bool Ativo { get; set; } = true;
        public int Versao { get; set; } = 1;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}
