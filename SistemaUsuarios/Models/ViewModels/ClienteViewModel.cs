using SistemaUsuarios.Models;

namespace SistemaUsuarios.Models.ViewModels
{
    // ── Lista de clientes ────────────────────────────────────────────────────
    public class ClienteListViewModel
    {
        public int TotalClientes { get; set; }
        public int TotalPropostas { get; set; }
        public int PropostasAprovadas { get; set; }
        public int ClientesEmViagem { get; set; }
        public List<AniversarioItem> AniversariosProximos { get; set; } = new();
        public List<ClienteResumoItem> Clientes { get; set; } = new();
        public string? TermoBusca { get; set; }
    }

    public class AniversarioItem
    {
        public Guid ClienteId { get; set; }
        public string Nome { get; set; } = "";
        public string? FotoPath { get; set; }
        public DateTime DataNascimento { get; set; }
        public int DiasAteAniversario { get; set; }
        public int IdadeQueVaiFazer { get; set; }
    }

    public class ClienteResumoItem
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = "";
        public string? FotoPath { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public int? Idade { get; set; }
        public int TotalPropostas { get; set; }
        public int PropostasAprovadas { get; set; }
        public bool EmViagem { get; set; }
        public DateTime DataCriacao { get; set; }
        public List<string> UltimosDestinos { get; set; } = new();
    }

    // ── Detalhe do cliente ───────────────────────────────────────────────────
    public class ClienteDetalheViewModel
    {
        // Dados básicos
        public Guid Id { get; set; }
        public string Nome { get; set; } = "";
        public string? FotoPath { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public Genero? Genero { get; set; }
        public string? Cpf { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataEntradaCliente { get; set; }

        // Endereço
        public string? Cep { get; set; }
        public string? Logradouro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }

        // Indicação
        public Guid? ClienteIndicadorId { get; set; }
        public string? ClienteIndicadorNome { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Métricas
        public int TotalPropostas { get; set; }
        public int PropostasAprovadas { get; set; }
        public int PropostasEnviadas { get; set; }
        public bool EmViagem { get; set; }

        // Conteúdo das abas
        public List<PropostaClienteItem> Propostas { get; set; } = new();
        public List<PassageiroRecorrenteItem> PassageirosRecorrentes { get; set; } = new();
        public List<EventoAgendaItem> Agenda { get; set; } = new();

        // Tab ativa
        public string ActiveTab { get; set; } = "resumo";

        // Computed
        public int? Idade => DataNascimento.HasValue
            ? (int?)CalcularIdade(DataNascimento.Value)
            : null;

        public string Iniciais => string.Join("",
            Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(p => char.ToUpper(p[0])));

        private static int CalcularIdade(DateTime nascimento)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - nascimento.Year;
            if (nascimento.Date > hoje.AddYears(-idade)) idade--;
            return idade;
        }
    }

    public class PropostaClienteItem
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = "";
        public StatusProposta StatusProposta { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public int NumeroPassageiros { get; set; }
        public int NumeroCriancas { get; set; }
        public string? FotoCapa { get; set; }
        public bool LinkPublicoAtivo { get; set; }
        public DateTime DataCriacao { get; set; }
        public List<string> Destinos { get; set; } = new();

        public int? DuracaoDias => DataInicio.HasValue && DataFim.HasValue
            ? (int?)((DataFim.Value - DataInicio.Value).Days + 1)
            : null;

        public bool EmAndamento => DataInicio.HasValue && DataFim.HasValue
            && DataInicio.Value.Date <= DateTime.Today
            && DataFim.Value.Date >= DateTime.Today;
    }

    public class PassageiroRecorrenteItem
    {
        public string Nome { get; set; } = "";
        public int Ocorrencias { get; set; }
        public RelacionamentoPassageiro? Relacionamento { get; set; }
        public DateTime? DataNascimento { get; set; }

        public int? Idade => DataNascimento.HasValue
            ? (int?)((DateTime.Today - DataNascimento.Value).Days / 365)
            : null;
    }

    public class EventoAgendaItem
    {
        /// <summary>aniversario | viagem_andamento | viagem_proxima | viagem_fim</summary>
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string? Subtitulo { get; set; }
        public DateTime Data { get; set; }
        public string Icone { get; set; } = "";
        public string CorCss { get; set; } = "";
        public Guid? PropostaId { get; set; }
    }
}
