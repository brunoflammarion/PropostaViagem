namespace SistemaUsuarios.Models
{
    public class AiUsageRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AgenciaId { get; set; }
        public Guid UsuarioId { get; set; }
        public Guid? PropostaId { get; set; }
        public string? EntidadeId { get; set; }
        public string? EntidadeTipo { get; set; }

        public string Funcionalidade { get; set; } = "";
        public string Provedor { get; set; } = "OpenAI";
        public string Modelo { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public string? ProviderRequestId { get; set; }

        public int InputTokens { get; set; }
        public int CachedInputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int ReasoningTokens { get; set; }
        public int TotalTokens { get; set; }

        // Preço snapshot no momento da chamada (USD por 1M tokens)
        public decimal PrecoInputPorMilhao { get; set; }
        public decimal PrecoCachedInputPorMilhao { get; set; }
        public decimal PrecoOutputPorMilhao { get; set; }

        public decimal CustoInput { get; set; }
        public decimal CustoCachedInput { get; set; }
        public decimal CustoOutput { get; set; }
        public decimal CustoTotal { get; set; }
        public string Moeda { get; set; } = "USD";

        public bool Sucesso { get; set; }
        public string Status { get; set; } = "";
        public int DuracaoMs { get; set; }
        public int? HttpStatusExterno { get; set; }
        public string? TipoErro { get; set; }
        public string? MensagemErroSanitizada { get; set; }

        public DateTime DataHoraInicio { get; set; }
        public DateTime DataHoraFim { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}
