using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels.PlatformAdmin;

namespace SistemaUsuarios.Services
{
    public class PlatformMetricsService
    {
        private readonly ApplicationDbContext _db;

        public PlatformMetricsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PlatformDashboardViewModel> GetDashboardMetrics()
        {
            var cutoff30d  = DateTime.Now.AddDays(-30);
            var cutoff12m  = DateTime.Now.AddMonths(-12);

            var vm = new PlatformDashboardViewModel
            {
                TotalAgencias       = await _db.Usuarios.CountAsync(u => u.TipoUsuario == TipoUsuario.Master),
                AgenciasAtivas      = await _db.Usuarios.CountAsync(u => u.TipoUsuario == TipoUsuario.Master && u.Status == StatusUsuario.Ativo),
                AgenciasBloqueadas  = await _db.Usuarios.CountAsync(u => u.TipoUsuario == TipoUsuario.Master && u.Status == StatusUsuario.Bloqueado),
                AgenciasNovas30d    = await _db.Usuarios.CountAsync(u => u.TipoUsuario == TipoUsuario.Master && u.DataCriacao >= cutoff30d),
                TotalUsuarios       = await _db.Usuarios.CountAsync(),
                TotalPropostas      = await _db.Propostas.CountAsync(),
                TotalClientes       = await _db.Clientes.CountAsync(c => !c.IsDeleted),
                TotalLeads          = await _db.Leads.CountAsync(),
                TotalVisualizacoes  = await _db.PropostaVisualizacoes.CountAsync(),
            };

            // Crescimento mensal — executa no DB, projeta DateTime em memória
            var rawGrowth = await _db.Usuarios
                .Where(u => u.TipoUsuario == TipoUsuario.Master && u.DataCriacao >= cutoff12m)
                .GroupBy(u => new { u.DataCriacao.Year, u.DataCriacao.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            vm.CrescimentoMensal = rawGrowth
                .Select(g => new GrowthDataPoint { Ano = g.Year, Mes = g.Month, Quantidade = g.Count })
                .ToList();

            // Top 5 agências por número de propostas
            vm.TopAgencias = await _db.Usuarios
                .Where(u => u.TipoUsuario == TipoUsuario.Master)
                .OrderByDescending(u => _db.Propostas.Count(p => p.UsuarioMasterId == u.Id))
                .Take(5)
                .Select(u => new AgenciaListItem
                {
                    Id             = u.Id,
                    NomeAgencia    = u.NomeAgencia ?? u.Nome,
                    SlugAgencia    = u.SlugAgencia,
                    NomeMaster     = u.Nome,
                    Status         = u.Status,
                    DataCriacao    = u.DataCriacao,
                    TotalPropostas     = _db.Propostas.Count(p => p.UsuarioMasterId == u.Id),
                    TotalVisualizacoes = _db.PropostaVisualizacoes.Count(pv => pv.Proposta.UsuarioMasterId == u.Id),
                })
                .ToListAsync();

            // Agências ativas sem proposta criada nos últimos 30 dias (risco de churn)
            var recentMasterIds = await _db.Propostas
                .Where(p => p.DataCriacao >= cutoff30d && p.UsuarioMasterId != null)
                .Select(p => p.UsuarioMasterId!.Value)
                .Distinct()
                .ToListAsync();

            vm.AgenciasSemAtividade = await _db.Usuarios
                .Where(u => u.TipoUsuario == TipoUsuario.Master
                         && u.Status == StatusUsuario.Ativo
                         && u.DataCriacao < cutoff30d
                         && !recentMasterIds.Contains(u.Id))
                .OrderByDescending(u => u.DataCriacao)
                .Take(10)
                .Select(u => new AgenciaListItem
                {
                    Id           = u.Id,
                    NomeAgencia  = u.NomeAgencia ?? u.Nome,
                    SlugAgencia  = u.SlugAgencia,
                    NomeMaster   = u.Nome,
                    Status       = u.Status,
                    DataCriacao  = u.DataCriacao,
                    UltimaAtividade = _db.Propostas
                        .Where(p => p.UsuarioMasterId == u.Id)
                        .Max(p => (DateTime?)p.DataCriacao),
                })
                .ToListAsync();

            return vm;
        }

        public async Task<AgenciaListViewModel> GetAgenciasList(string? filtroStatus)
        {
            var query = _db.Usuarios.Where(u => u.TipoUsuario == TipoUsuario.Master);

            query = filtroStatus switch
            {
                "ativa"     => query.Where(u => u.Status == StatusUsuario.Ativo),
                "bloqueada" => query.Where(u => u.Status == StatusUsuario.Bloqueado),
                "inativa"   => query.Where(u => u.Status == StatusUsuario.Inativo),
                "nova"      => query.Where(u => u.Status == StatusUsuario.Novo),
                _           => query
            };

            var agencias = await query
                .OrderByDescending(u => u.DataCriacao)
                .Select(u => new AgenciaListItem
                {
                    Id                 = u.Id,
                    NomeAgencia        = u.NomeAgencia ?? u.Nome,
                    SlugAgencia        = u.SlugAgencia,
                    NomeMaster         = u.Nome,
                    Status             = u.Status,
                    DataCriacao        = u.DataCriacao,
                    TotalAssociados    = _db.Usuarios.Count(a => a.UsuarioMasterId == u.Id),
                    TotalPropostas     = _db.Propostas.Count(p => p.UsuarioMasterId == u.Id),
                    TotalClientes      = _db.Clientes.Count(c => c.UsuarioId == u.Id && !c.IsDeleted),
                    TotalLeads         = _db.Leads.Count(l => l.UsuarioId == u.Id),
                    TotalVisualizacoes = _db.PropostaVisualizacoes.Count(pv => pv.Proposta.UsuarioMasterId == u.Id),
                    UltimaAtividade    = _db.Propostas
                        .Where(p => p.UsuarioMasterId == u.Id)
                        .Max(p => (DateTime?)p.DataCriacao),
                })
                .ToListAsync();

            return new AgenciaListViewModel
            {
                Agencias     = agencias,
                FiltroStatus = filtroStatus,
                TotalCount   = agencias.Count,
            };
        }

        public async Task<AgenciaDetalheViewModel?> GetAgenciaDetail(Guid masterId)
        {
            var master = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == masterId && u.TipoUsuario == TipoUsuario.Master);

            if (master == null) return null;

            var cutoff90d = DateTime.Now.AddDays(-90);

            var associados = await _db.Usuarios
                .Where(u => u.UsuarioMasterId == masterId)
                .OrderBy(u => u.Nome)
                .Select(u => new AssociadoItem
                {
                    Id          = u.Id,
                    Nome        = u.Nome,
                    Status      = u.Status,
                    DataCriacao = u.DataCriacao,
                })
                .ToListAsync();

            // Atividade dos últimos 90 dias — projeção de DateTime feita em memória
            var rawAtividade = await _db.Propostas
                .Where(p => p.UsuarioMasterId == masterId && p.DataCriacao >= cutoff90d)
                .GroupBy(p => new { p.DataCriacao.Year, p.DataCriacao.Month, p.DataCriacao.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                .ToListAsync();

            return new AgenciaDetalheViewModel
            {
                MasterId           = master.Id,
                NomeAgencia        = master.NomeAgencia ?? master.Nome,
                SlugAgencia        = master.SlugAgencia,
                NomeMaster         = master.Nome,
                Status             = master.Status,
                DataCriacao        = master.DataCriacao,
                TotalAssociados    = associados.Count,
                Associados         = associados,
                TotalPropostas     = await _db.Propostas.CountAsync(p => p.UsuarioMasterId == masterId),
                TotalClientes      = await _db.Clientes.CountAsync(c => c.UsuarioId == masterId && !c.IsDeleted),
                TotalLeads         = await _db.Leads.CountAsync(l => l.UsuarioId == masterId),
                TotalVisualizacoes = await _db.PropostaVisualizacoes.CountAsync(pv => pv.Proposta.UsuarioMasterId == masterId),
                TotalOfertas       = await _db.Ofertas.CountAsync(o => o.UsuarioMasterId == masterId),
                TotalTarefas       = await _db.Tarefas.CountAsync(t => t.UsuarioId == masterId && !t.IsDeleted),
                Atividade90d       = rawAtividade
                    .Select(g => new AtividadeDataPoint
                    {
                        Data      = new DateTime(g.Year, g.Month, g.Day),
                        Propostas = g.Count,
                    })
                    .ToList(),
            };
        }
    }
}
