using SistemaUsuarios.Models;

namespace SistemaUsuarios.Models.ViewModels.PlatformAdmin
{
    public class ListaVipListaViewModel
    {
        public const int PageSize = 25;

        public List<ListaVipCadastro> Cadastros     { get; set; } = new();
        public int  TotalGeral      { get; set; }
        public int  TotalHoje       { get; set; }
        public int  Total7d         { get; set; }
        public int  Total30d        { get; set; }
        public int  TotalNovos      { get; set; }
        public int  TotalFiltrado   { get; set; }
        public int  PaginaAtual     { get; set; }
        public int  TotalPaginas    { get; set; }
        public string? Busca           { get; set; }
        public string? Periodo         { get; set; }
        public string? FiltroPropostas { get; set; }
        public string? Ordem           { get; set; }

        // Rótulo legível de PropostasPorMes
        public static string PropostasLabel(string? valor) => valor switch
        {
            "ate20"  => "Até 20/mês",
            "20-50"  => "20 a 50/mês",
            "50-100" => "50 a 100/mês",
            "100+"   => "Acima de 100/mês",
            _        => "—"
        };

        // Peso de ordenação para propostas (usado em sort in-memory)
        public static int PropostasOrdem(string? valor) => valor switch
        {
            "ate20"  => 1,
            "20-50"  => 2,
            "50-100" => 3,
            "100+"   => 4,
            _        => 0
        };
    }
}
