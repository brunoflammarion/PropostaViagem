using System.ComponentModel.DataAnnotations;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Models.ViewModels
{
    public class PropostaFiltroViewModel
    {
        // === FILTROS ===
        [Display(Name = "Buscar")]
        public string? TermoBusca { get; set; }

        [Display(Name = "Status")]
        public StatusProposta? FiltroStatus { get; set; }

        [Display(Name = "Data Início")]
        [DataType(DataType.Date)]
        public DateTime? DataInicioFiltro { get; set; }

        [Display(Name = "Data Fim")]
        [DataType(DataType.Date)]
        public DateTime? DataFimFiltro { get; set; }

        [Display(Name = "Usuário")]
        public Guid? FiltroUsuario { get; set; }

        [Display(Name = "Link Público")]
        public bool? FiltroLinkAtivo { get; set; }

        // === DADOS PARA EXIBIÇÃO ===
        public List<PropostaListViewModel> Propostas { get; set; } = new List<PropostaListViewModel>();
        public List<Usuario> Usuarios { get; set; } = new List<Usuario>();

        // === ESTATÍSTICAS ===
        public int TotalPropostas { get; set; }
        public int TotalRascunhos { get; set; }
        public int TotalEnviadas { get; set; }
        public int TotalAprovadas { get; set; }
        public int TotalRejeitadas { get; set; }
        public int TotalCanceladas { get; set; }

        // === PROPRIEDADES CALCULADAS ===

        /// <summary>
        /// Taxa de aprovação das propostas
        /// </summary>
        public double TaxaAprovacao
        {
            get
            {
                if (TotalPropostas == 0) return 0;
                return (double)TotalAprovadas / TotalPropostas * 100;
            }
        }

        /// <summary>
        /// Taxa de conversão (enviadas que foram aprovadas)
        /// </summary>
        public double TaxaConversao
        {
            get
            {
                var totalEnviadasOuAprovadas = TotalEnviadas + TotalAprovadas;
                if (totalEnviadasOuAprovadas == 0) return 0;
                return (double)TotalAprovadas / totalEnviadasOuAprovadas * 100;
            }
        }

        /// <summary>
        /// Propostas em andamento (rascunhos + enviadas)
        /// </summary>
        public int PropostasEmAndamento => TotalRascunhos + TotalEnviadas;

        /// <summary>
        /// Propostas finalizadas (aprovadas + rejeitadas + canceladas)
        /// </summary>
        public int PropostasFinalizadas => TotalAprovadas + TotalRejeitadas + TotalCanceladas;

        /// <summary>
        /// Indica se há filtros aplicados
        /// </summary>
        public bool TemFiltrosAplicados
        {
            get
            {
                return !string.IsNullOrEmpty(TermoBusca) ||
                       FiltroStatus.HasValue ||
                       DataInicioFiltro.HasValue ||
                       DataFimFiltro.HasValue ||
                       FiltroUsuario.HasValue ||
                       FiltroLinkAtivo.HasValue;
            }
        }

        /// <summary>
        /// Descrição dos filtros aplicados
        /// </summary>
        public string DescricaoFiltros
        {
            get
            {
                var filtros = new List<string>();

                if (!string.IsNullOrEmpty(TermoBusca))
                    filtros.Add($"Busca: '{TermoBusca}'");

                if (FiltroStatus.HasValue)
                    filtros.Add($"Status: {FiltroStatus.Value}");

                if (DataInicioFiltro.HasValue)
                    filtros.Add($"A partir de: {DataInicioFiltro.Value:dd/MM/yyyy}");

                if (DataFimFiltro.HasValue)
                    filtros.Add($"Até: {DataFimFiltro.Value:dd/MM/yyyy}");

                if (FiltroUsuario.HasValue)
                {
                    var usuario = Usuarios.FirstOrDefault(u => u.Id == FiltroUsuario.Value);
                    if (usuario != null)
                        filtros.Add($"Usuário: {usuario.Nome}");
                }

                if (FiltroLinkAtivo.HasValue)
                    filtros.Add($"Link: {(FiltroLinkAtivo.Value ? "Ativo" : "Inativo")}");

                return filtros.Any() ? string.Join(" | ", filtros) : "Nenhum filtro aplicado";
            }
        }

        /// <summary>
        /// Estatísticas por status para gráficos
        /// </summary>
        public Dictionary<string, int> EstatisticasPorStatus
        {
            get
            {
                return new Dictionary<string, int>
                {
                    { "Rascunho", TotalRascunhos },
                    { "Enviada", TotalEnviadas },
                    { "Aprovada", TotalAprovadas },
                    { "Rejeitada", TotalRejeitadas },
                    { "Cancelada", TotalCanceladas }
                };
            }
        }

        // === MÉTODOS AUXILIARES ===

        /// <summary>
        /// Limpa todos os filtros
        /// </summary>
        public void LimparFiltros()
        {
            TermoBusca = null;
            FiltroStatus = null;
            DataInicioFiltro = null;
            DataFimFiltro = null;
            FiltroUsuario = null;
            FiltroLinkAtivo = null;
        }

        /// <summary>
        /// Aplica filtros padrão
        /// </summary>
        public void AplicarFiltrosPadrao()
        {
            // Exemplo: mostrar apenas propostas dos últimos 30 dias
            if (!DataInicioFiltro.HasValue && !DataFimFiltro.HasValue)
            {
                DataInicioFiltro = DateTime.Now.AddDays(-30);
            }
        }

        /// <summary>
        /// Valida se as datas dos filtros são válidas
        /// </summary>
        /// <returns>True se válidas, False caso contrário</returns>
        public bool ValidarFiltrosDatas()
        {
            if (DataInicioFiltro.HasValue && DataFimFiltro.HasValue)
            {
                return DataInicioFiltro.Value <= DataFimFiltro.Value;
            }
            return true;
        }

        /// <summary>
        /// Obtém mensagem de erro para datas inválidas
        /// </summary>
        /// <returns>Mensagem de erro ou null se válidas</returns>
        public string? ObterErroValidacaoFiltros()
        {
            if (!ValidarFiltrosDatas())
            {
                return "Data de fim do filtro deve ser posterior ou igual à data de início";
            }
            return null;
        }

        /// <summary>
        /// Conta propostas por período
        /// </summary>
        /// <param name="dias">Número de dias para contar</param>
        /// <returns>Quantidade de propostas no período</returns>
        public int ContarPropostasUltimosDias(int dias)
        {
            var dataLimite = DateTime.Now.AddDays(-dias);
            return Propostas.Count(p => p.DataCriacao >= dataLimite);
        }

        /// <summary>
        /// Obtém as propostas mais recentes
        /// </summary>
        /// <param name="quantidade">Quantidade de propostas</param>
        /// <returns>Lista das propostas mais recentes</returns>
        public List<PropostaListViewModel> ObterPropostasRecentes(int quantidade = 5)
        {
            return Propostas
                .OrderByDescending(p => p.DataCriacao)
                .Take(quantidade)
                .ToList();
        }

        /// <summary>
        /// Obtém propostas que precisam de atenção
        /// </summary>
        /// <returns>Lista de propostas que precisam de atenção</returns>
        public List<PropostaListViewModel> ObterPropostasAtencao()
        {
            var diasLimite = 7; // Propostas sem modificação há mais de 7 dias
            var dataLimite = DateTime.Now.AddDays(-diasLimite);

            return Propostas
                .Where(p =>
                    (p.StatusProposta == StatusProposta.Rascunho || p.StatusProposta == StatusProposta.Enviada) &&
                    (p.DataModificacao ?? p.DataCriacao) < dataLimite)
                .OrderBy(p => p.DataModificacao ?? p.DataCriacao)
                .ToList();
        }

        /// <summary>
        /// Resumo executivo das propostas
        /// </summary>
        public string ResumoExecutivo
        {
            get
            {
                if (TotalPropostas == 0)
                    return "Nenhuma proposta criada ainda.";

                var resumo = $"Total de {TotalPropostas} proposta(s). ";

                if (TotalAprovadas > 0)
                    resumo += $"{TotalAprovadas} aprovada(s) ({TaxaAprovacao:F1}% de taxa). ";

                if (PropostasEmAndamento > 0)
                    resumo += $"{PropostasEmAndamento} em andamento. ";

                var propostas30Dias = ContarPropostasUltimosDias(30);
                if (propostas30Dias > 0)
                    resumo += $"{propostas30Dias} criada(s) nos últimos 30 dias.";

                return resumo.Trim();
            }
        }

        /// <summary>
        /// Converte para string para debug
        /// </summary>
        /// <returns>Representação string do objeto</returns>
        public override string ToString()
        {
            return $"PropostaFiltro: {TotalPropostas} propostas | Filtros: {(TemFiltrosAplicados ? "Sim" : "Não")}";
        }
    }
}