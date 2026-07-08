using System.Text.Json.Serialization;

namespace SistemaUsuarios.Models.Dto
{
    public class ImportacaoPreviewDto
    {
        [JsonPropertyName("proposta")]
        public PropostaImportDto? Proposta { get; set; }

        [JsonPropertyName("passageiros")]
        public List<PassageiroImportDto> Passageiros { get; set; } = new();

        [JsonPropertyName("voos")]
        public List<VooImportDto> Voos { get; set; } = new();

        [JsonPropertyName("destinos")]
        public List<DestinoImportDto> Destinos { get; set; } = new();

        [JsonPropertyName("seguros")]
        public List<SeguroImportDto> Seguros { get; set; } = new();

        [JsonPropertyName("valoresFinanceiros")]
        public ValoresFinanceirosImportDto? ValoresFinanceiros { get; set; }
    }

    public class PropostaImportDto
    {
        [JsonPropertyName("titulo")]
        public string? Titulo { get; set; }

        [JsonPropertyName("observacoesGerais")]
        public string? ObservacoesGerais { get; set; }

        [JsonPropertyName("operadora")]
        public string? Operadora { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class PassageiroImportDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("dataNascimento")]
        public string? DataNascimento { get; set; }

        [JsonPropertyName("genero")]
        public string? Genero { get; set; }

        [JsonPropertyName("relacionamento")]
        public string? Relacionamento { get; set; }

        [JsonPropertyName("observacoes")]
        public string? Observacoes { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class VooImportDto
    {
        [JsonPropertyName("numeroVoo")]
        public string NumeroVoo { get; set; } = "";

        [JsonPropertyName("tipoVoo")]
        public string TipoVoo { get; set; } = "Ida";

        [JsonPropertyName("companhia")]
        public string Companhia { get; set; } = "";

        [JsonPropertyName("classe")]
        public string? Classe { get; set; }

        [JsonPropertyName("duracao")]
        public string? Duracao { get; set; }

        [JsonPropertyName("origem")]
        public string Origem { get; set; } = "";

        [JsonPropertyName("destino")]
        public string Destino { get; set; } = "";

        [JsonPropertyName("horarioSaida")]
        public string? HorarioSaida { get; set; }

        [JsonPropertyName("horarioChegada")]
        public string? HorarioChegada { get; set; }

        [JsonPropertyName("bagagemMaoPeso")]
        public decimal? BagagemMaoPeso { get; set; }

        [JsonPropertyName("bagagemDespachadaPeso")]
        public decimal? BagagemDespachadaPeso { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class DestinoImportDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("pais")]
        public string? Pais { get; set; }

        [JsonPropertyName("cidade")]
        public string? Cidade { get; set; }

        [JsonPropertyName("dataChegada")]
        public string? DataChegada { get; set; }

        [JsonPropertyName("dataSaida")]
        public string? DataSaida { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("hospedagens")]
        public List<HospedagemImportDto> Hospedagens { get; set; } = new();

        [JsonPropertyName("experiencias")]
        public List<ExperienciaImportDto> Experiencias { get; set; } = new();

        [JsonPropertyName("transportes")]
        public List<TransporteImportDto> Transportes { get; set; } = new();

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class HospedagemImportDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("endereco")]
        public string? Endereco { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("checkIn")]
        public string? CheckIn { get; set; }

        [JsonPropertyName("checkOut")]
        public string? CheckOut { get; set; }

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; } = "Hotel";

        [JsonPropertyName("tipoPensao")]
        public string TipoPensao { get; set; } = "SemPensao";

        [JsonPropertyName("reserva")]
        public string? Reserva { get; set; }

        [JsonPropertyName("observacoes")]
        public string? Observacoes { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class ExperienciaImportDto
    {
        [JsonPropertyName("tipoPasseio")]
        public string TipoPasseio { get; set; } = "";

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("valor")]
        public decimal? Valor { get; set; }

        [JsonPropertyName("dataInicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("dataFim")]
        public string? DataFim { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class TransporteImportDto
    {
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = "";

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("valor")]
        public decimal? Valor { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class SeguroImportDto
    {
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = "";

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }

        [JsonPropertyName("valor")]
        public decimal? Valor { get; set; }

        [JsonPropertyName("incluir")]
        public bool Incluir { get; set; } = true;
    }

    public class ValoresFinanceirosImportDto
    {
        [JsonPropertyName("valorTotal")]
        public decimal? ValorTotal { get; set; }

        [JsonPropertyName("observacoes")]
        public string? Observacoes { get; set; }
    }

    public class ConfirmarImportacaoRequest
    {
        [JsonPropertyName("propostaId")]
        public Guid PropostaId { get; set; }

        [JsonPropertyName("preview")]
        public ImportacaoPreviewDto Preview { get; set; } = new();
    }

    // ── Camada intermediária de interpretação da IA ───────────────────────────
    // Representa o ENTENDIMENTO da IA sobre o documento, nunca persistido diretamente.
    // O chat trabalha sobre este objeto; a persistência só ocorre após confirmação.
    public class TravelProposalDraft
    {
        [JsonPropertyName("mensagemInicial")]
        public string MensagemInicial { get; set; } = "";

        [JsonPropertyName("pendentes")]
        public List<string> Pendentes { get; set; } = new();

        [JsonPropertyName("alertas")]
        public List<string> Alertas { get; set; } = new();

        [JsonPropertyName("confiancaGeral")]
        public int ConfiancaGeral { get; set; } = 80;

        [JsonPropertyName("proposta")]
        public PropostaImportDto? Proposta { get; set; }

        [JsonPropertyName("passageiros")]
        public List<PassageiroImportDto> Passageiros { get; set; } = new();

        [JsonPropertyName("voos")]
        public List<VooImportDto> Voos { get; set; } = new();

        [JsonPropertyName("destinos")]
        public List<DestinoImportDto> Destinos { get; set; } = new();

        [JsonPropertyName("seguros")]
        public List<SeguroImportDto> Seguros { get; set; } = new();

        [JsonPropertyName("valoresFinanceiros")]
        public ValoresFinanceirosImportDto? ValoresFinanceiros { get; set; }

        public ImportacaoPreviewDto ToPreview() => new()
        {
            Proposta = Proposta,
            Passageiros = Passageiros,
            Voos = Voos,
            Destinos = Destinos,
            Seguros = Seguros,
            ValoresFinanceiros = ValoresFinanceiros
        };
    }

    // ── Confirmação bloco a bloco ─────────────────────────────────────────────
    public class ConfirmarBlocoRequest
    {
        [JsonPropertyName("propostaId")]
        public Guid PropostaId { get; set; }

        // "proposta" | "passageiros" | "voos" | "destinos" | "seguros" | "todos"
        [JsonPropertyName("bloco")]
        public string Bloco { get; set; } = "";

        [JsonPropertyName("proposta")]
        public PropostaImportDto? Proposta { get; set; }

        [JsonPropertyName("passageiros")]
        public List<PassageiroImportDto>? Passageiros { get; set; }

        [JsonPropertyName("voos")]
        public List<VooImportDto>? Voos { get; set; }

        [JsonPropertyName("destinos")]
        public List<DestinoImportDto>? Destinos { get; set; }

        [JsonPropertyName("seguros")]
        public List<SeguroImportDto>? Seguros { get; set; }

        [JsonPropertyName("valoresFinanceiros")]
        public ValoresFinanceirosImportDto? ValoresFinanceiros { get; set; }
    }

    public class ResultadoBloco
    {
        public bool Ok { get; set; }
        public string? Erro { get; set; }
        public string Bloco { get; set; } = "";
        public int Itens { get; set; }
        public string Mensagem { get; set; } = "";
    }

    public class ResultadoImportacao
    {
        public bool Ok { get; set; }
        public string? Erro { get; set; }
        public int Passageiros { get; set; }
        public int Voos { get; set; }
        public int Destinos { get; set; }
        public int Hospedagens { get; set; }
        public int Experiencias { get; set; }
        public int Transportes { get; set; }
        public int Seguros { get; set; }
    }
}
