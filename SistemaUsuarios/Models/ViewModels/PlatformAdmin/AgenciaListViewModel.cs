namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    public class AgenciaListViewModel
    {
        public List<AgenciaListItem> Agencias { get; set; } = new();
        public string? FiltroStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class AgenciaListItem
    {
        public Guid Id { get; set; }
        public string NomeAgencia { get; set; } = string.Empty;
        public string? SlugAgencia { get; set; }
        public string NomeMaster { get; set; } = string.Empty;
        public StatusUsuario Status { get; set; }
        public DateTime DataCriacao { get; set; }
        public int TotalAssociados { get; set; }
        public int TotalPropostas { get; set; }
        public int TotalClientes { get; set; }
        public int TotalLeads { get; set; }
        public int TotalVisualizacoes { get; set; }
        public DateTime? UltimaAtividade { get; set; }

        public string StatusLabel => Status switch
        {
            StatusUsuario.Ativo => "Ativa",
            StatusUsuario.Bloqueado => "Bloqueada",
            StatusUsuario.Inativo => "Inativa",
            StatusUsuario.Novo => "Nova",
            _ => "–"
        };

        public string StatusCss => Status switch
        {
            StatusUsuario.Ativo => "success",
            StatusUsuario.Bloqueado => "danger",
            StatusUsuario.Inativo => "secondary",
            StatusUsuario.Novo => "warning",
            _ => "secondary"
        };
    }
}
