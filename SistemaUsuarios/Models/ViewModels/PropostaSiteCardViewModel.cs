namespace SistemaUsuarios.Models.ViewModels
{
    /// <summary>
    /// Whitelist de informações públicas de uma proposta Site para exibição
    /// na vitrine da agência. Nunca expõe dados de cliente, passageiros ou valores.
    /// </summary>
    public class PropostaSiteCardViewModel
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = "";
        public string? FotoCapa { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }

        // Destinos (nome/cidade para exibição — sem dados privados)
        public List<string> Destinos { get; set; } = new();

        // Indicadores dinâmicos de composição (só presentes se existirem)
        public bool TemVoo { get; set; }
        public bool TemHospedagem { get; set; }
        public bool TemExperiencia { get; set; }
        public bool TemTransporte { get; set; }
        public bool TemSeguro { get; set; }

        // Dados derivados (calculados no controller, sem expor entidades)
        public int? DuracaoNoites => (DataInicio.HasValue && DataFim.HasValue)
            ? (int?)(DataFim.Value.Date - DataInicio.Value.Date).TotalDays
            : null;

        public string? MesAno => DataInicio.HasValue
            ? DataInicio.Value.ToString("MMM · yyyy", new System.Globalization.CultureInfo("pt-BR")).ToUpper()
            : null;

        public string DestinosPrincipal => Destinos.Count > 0
            ? string.Join(" · ", Destinos.Take(2))
            : "";
    }
}
