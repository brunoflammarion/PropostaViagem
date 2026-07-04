using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemaUsuarios.Models.ViewModels
{
    public class TarefaIndexViewModel
    {
        public List<Tarefa> TarefasHoje      { get; set; } = new();
        public List<Tarefa> TarefasAtrasadas { get; set; } = new();
        public List<Tarefa> TarefasSemana    { get; set; } = new();
        public List<Tarefa> TarefasConcluidas { get; set; } = new();

        public int TotalHoje      => TarefasHoje.Count;
        public int TotalAtrasadas => TarefasAtrasadas.Count;
        public int TotalSemana    => TarefasSemana.Count;

        // Para o modal de criação
        public List<SelectListItem> Clientes  { get; set; } = new();
        public List<SelectListItem> Propostas { get; set; } = new();
    }

    public class ConfiguracaoLembreteViewModel
    {
        public Guid   Id             { get; set; }
        public string TemplateCodigo { get; set; } = "";
        public string Titulo         { get; set; } = "";
        public string Descricao      { get; set; } = "";
        public string Tipo           { get; set; } = "";
        public bool   Habilitado     { get; set; }
        public int?   OffsetDias     { get; set; }
        public string? MomentoReferencia { get; set; }
        public string TextoQuando    { get; set; } = "";
    }
}
