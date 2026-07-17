namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    public class PlatformDashboardViewModel
    {
        public int TotalAgencias { get; set; }
        public int AgenciasAtivas { get; set; }
        public int AgenciasBloqueadas { get; set; }
        public int AgenciasNovas30d { get; set; }
        public int TotalUsuarios { get; set; }
        public int TotalPropostas { get; set; }
        public int TotalClientes { get; set; }
        public int TotalLeads { get; set; }
        public int TotalVisualizacoes { get; set; }
        public List<GrowthDataPoint> CrescimentoMensal { get; set; } = new();
        public List<AgenciaListItem> TopAgencias { get; set; } = new();
        public List<AgenciaListItem> AgenciasSemAtividade { get; set; } = new();
    }

    public class GrowthDataPoint
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public int Quantidade { get; set; }
        public string Label => $"{Mes:D2}/{Ano}";
    }
}
