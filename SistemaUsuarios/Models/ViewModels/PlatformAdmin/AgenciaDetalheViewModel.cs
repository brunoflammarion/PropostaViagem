namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    public class AgenciaDetalheViewModel
    {
        public Guid MasterId { get; set; }
        public string NomeAgencia { get; set; } = string.Empty;
        public string? SlugAgencia { get; set; }
        public string NomeMaster { get; set; } = string.Empty;
        public StatusUsuario Status { get; set; }
        public DateTime DataCriacao { get; set; }

        public int TotalAssociados { get; set; }
        public List<AssociadoItem> Associados { get; set; } = new();

        public int TotalPropostas { get; set; }
        public int TotalClientes { get; set; }
        public int TotalLeads { get; set; }
        public int TotalVisualizacoes { get; set; }
        public int TotalOfertas { get; set; }
        public int TotalTarefas { get; set; }

        public List<AtividadeDataPoint> Atividade90d { get; set; } = new();

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

    public class AssociadoItem
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public StatusUsuario Status { get; set; }
        public DateTime DataCriacao { get; set; }

        public string StatusCss => Status switch
        {
            StatusUsuario.Ativo => "success",
            StatusUsuario.Bloqueado => "danger",
            StatusUsuario.Inativo => "secondary",
            StatusUsuario.Novo => "warning",
            _ => "secondary"
        };
    }

    public class AtividadeDataPoint
    {
        public DateTime Data { get; set; }
        public int Propostas { get; set; }
    }
}
