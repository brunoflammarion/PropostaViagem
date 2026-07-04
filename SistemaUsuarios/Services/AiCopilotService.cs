using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SistemaUsuarios.Services
{
    // ── Request / Response models ────────────────────────────────────────────────

    public class CopilotChatRequest
    {
        public Guid? PropostaId { get; set; }
        public string Message { get; set; } = "";
        public List<CopilotHistoryMessage> History { get; set; } = new();
        public string AbaAtual { get; set; } = "dados";

        /// <summary>Used only when PropostaId is null (Criar page).</summary>
        public PropostaContext? FormContext { get; set; }
    }

    public class CopilotHistoryMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
    }

    public class DestinoContextItem
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string? Pais { get; set; }
        public string? DataChegada { get; set; }
        public string? DataSaida { get; set; }
        public List<string> Hospedagens { get; set; } = new();
        public List<string> Experiencias { get; set; } = new();
        public List<string> Transportes { get; set; } = new();
    }

    public class PropostaContext
    {
        public Guid? PropostaId { get; set; }
        public bool IsNovaProposta { get; set; }
        public string? Status { get; set; }
        public string AbaAtual { get; set; } = "dados";

        // Dados gerais
        public string? Titulo { get; set; }
        public string? DataInicio { get; set; }
        public string? DataFim { get; set; }
        public int NumeroPassageiros { get; set; } = 1;
        public int NumeroCriancas { get; set; }
        public bool TemObservacoesGerais { get; set; }
        public string? ObservacoesTexto { get; set; }
        public bool TemFotoCapa { get; set; }
        public bool TemResumo { get; set; }
        public string? ResumoTexto { get; set; }

        // Cliente
        public bool TemCliente { get; set; }
        public string? NomeCliente { get; set; }

        // Conteúdo rico
        public List<string> Passageiros { get; set; } = new();
        public List<string> Voos { get; set; } = new();
        public List<DestinoContextItem> Destinos { get; set; } = new();
        public List<string> Seguros { get; set; } = new();

        // Contagens rápidas
        public int TotalPassageirosAdicionados { get; set; }
        public int TotalVoos { get; set; }
        public int TotalDestinos { get; set; }
        public int TotalHospedagens { get; set; }
        public int TotalExperiencias { get; set; }
        public int TotalTransportes { get; set; }
        public int TotalSeguros { get; set; }

        public static PropostaContext FromProposta(SistemaUsuarios.Models.Proposta p, string abaAtual)
        {
            var passageiros = p.PassageirosProposta?
                .OrderBy(pp => pp.Ordem)
                .Select(pp =>
                {
                    var partes = new List<string> { pp.Nome };
                    if (pp.IdadeCalculada.HasValue) partes.Add($"{pp.IdadeCalculada} anos");
                    if (pp.Relacionamento.HasValue) partes.Add(pp.Relacionamento.ToString()!);
                    return string.Join(", ", partes);
                })
                .ToList() ?? new List<string>();

            var voos = p.Voos?
                .OrderBy(v => v.HorarioSaida)
                .Select(v =>
                {
                    var trecho = $"{v.TipoVoo}: {v.Companhia} {v.NumeroVoo} {v.Origem}→{v.Destino}";
                    if (v.HorarioSaida.HasValue) trecho += $" {v.HorarioSaida:dd/MM HH:mm}";
                    return trecho;
                })
                .ToList() ?? new List<string>();

            var destinos = p.Destinos?
                .OrderBy(d => d.Ordem)
                .Select(d => new DestinoContextItem
                {
                    Id = d.Id.ToString(),
                    Nome = d.Nome,
                    Pais = d.Pais,
                    DataChegada = d.DataChegada?.ToString("yyyy-MM-dd"),
                    DataSaida = d.DataSaida?.ToString("yyyy-MM-dd"),
                    Hospedagens = d.Hospedagens?
                        .Select(h => $"{h.Nome} ({h.Categoria}, {h.TipoPensao})" +
                            (h.CheckIn.HasValue ? $" check-in {h.CheckIn:dd/MM} → {h.CheckOut:dd/MM}" : ""))
                        .ToList() ?? new List<string>(),
                    Experiencias = d.Experiencias?
                        .Select(e => string.IsNullOrWhiteSpace(e.TipoPasseio) ? (e.Descricao ?? "Experiência") : e.TipoPasseio)
                        .ToList() ?? new List<string>(),
                    Transportes = d.Transportes?
                        .Select(t => t.Titulo)
                        .ToList() ?? new List<string>(),
                })
                .ToList() ?? new List<DestinoContextItem>();

            var seguros = p.Seguros?
                .Select(s => s.Titulo)
                .ToList() ?? new List<string>();

            return new PropostaContext
            {
                PropostaId = p.Id,
                IsNovaProposta = false,
                Status = p.StatusProposta.ToString(),
                AbaAtual = abaAtual,
                Titulo = p.Titulo,
                DataInicio = p.DataInicio?.ToString("yyyy-MM-dd"),
                DataFim = p.DataFim?.ToString("yyyy-MM-dd"),
                NumeroPassageiros = p.NumeroPassageiros,
                NumeroCriancas = p.NumeroCriancas,
                TemObservacoesGerais = !string.IsNullOrWhiteSpace(p.ObservacoesGerais),
                ObservacoesTexto = p.ObservacoesGerais,
                TemFotoCapa = !string.IsNullOrWhiteSpace(p.FotoCapa),
                TemResumo = !string.IsNullOrWhiteSpace(p.ResumoProposta),
                ResumoTexto = p.ResumoProposta,
                TemCliente = p.Cliente != null,
                NomeCliente = p.Cliente?.Nome,
                Passageiros = passageiros,
                Voos = voos,
                Destinos = destinos,
                Seguros = seguros,
                TotalPassageirosAdicionados = passageiros.Count,
                TotalVoos = voos.Count,
                TotalDestinos = destinos.Count,
                TotalHospedagens = destinos.Sum(d => d.Hospedagens.Count),
                TotalExperiencias = destinos.Sum(d => d.Experiencias.Count),
                TotalTransportes = destinos.Sum(d => d.Transportes.Count),
                TotalSeguros = seguros.Count,
            };
        }
    }

    public class CopilotResponse
    {
        [JsonPropertyName("mensagem")]
        public string Mensagem { get; set; } = "";

        [JsonPropertyName("pontosFort")]
        public List<string> PontosFort { get; set; } = new();

        [JsonPropertyName("gaps")]
        public List<string> Gaps { get; set; } = new();

        [JsonPropertyName("sugestoes")]
        public List<string> Sugestoes { get; set; } = new();

        [JsonPropertyName("proximaPergunta")]
        public string? ProximaPergunta { get; set; }
    }

    // ── Service ──────────────────────────────────────────────────────────────────

    public class AiCopilotService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        public AiCopilotService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["OpenAI:ApiKey"] ?? "";
            _model = "gpt-4o-mini";
        }

        public async Task<CopilotResponse> ChatAsync(CopilotChatRequest request, PropostaContext ctx)
        {
            var systemPrompt = BuildSystemPrompt(ctx);

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            foreach (var msg in request.History.TakeLast(10))
                messages.Add(new { role = msg.Role, content = msg.Content });

            messages.Add(new { role = "user", content = request.Message });

            var payload = new
            {
                model = _model,
                messages,
                response_format = new { type = "json_object" },
                max_tokens = 1200,
                temperature = 0.4
            };

            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var httpRes = await _http.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var resJson = await httpRes.Content.ReadAsStringAsync();

            if (!httpRes.IsSuccessStatusCode)
                return ErrorResponse($"Erro na API OpenAI ({httpRes.StatusCode}).");

            try
            {
                var doc = JsonDocument.Parse(resJson);
                var aiContent = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "{}";

                var result = JsonSerializer.Deserialize<CopilotResponse>(aiContent, _jsonOpts);
                return result ?? ErrorResponse("Resposta inválida do modelo.");
            }
            catch (Exception ex)
            {
                return ErrorResponse($"Erro ao interpretar resposta: {ex.Message}");
            }
        }

        private static CopilotResponse ErrorResponse(string msg) =>
            new() { Mensagem = msg, Sugestoes = new List<string> { "Tentar novamente" } };

        private static string BuildSystemPrompt(PropostaContext ctx)
        {
            var ctxJson = JsonSerializer.Serialize(ctx, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var formatExemplo = @"{
  ""mensagem"": ""análise rica e específica como um especialista consultivo falando (2-4 frases)"",
  ""pontosFort"": [""ponto forte comercial desta proposta específica"", ""diferencial que encanta o cliente""],
  ""gaps"": [""lacuna que pode gerar objeção ou insegurança no cliente"", ""oportunidade de melhoria concreta""],
  ""sugestoes"": [""próxima ação consultiva sugerida ao agente"", ""sugestão de valor real""],
  ""proximaPergunta"": ""pergunta provocativa que faz o agente pensar melhor a proposta (ou null)""
}";

            return
$@"Você é o Copiloto da Proposta — especialista sênior em turismo de alto padrão e consultor de vendas para agentes de viagens brasileiros.

## Quem você é
Você tem profundo conhecimento em destinos turísticos, hotelaria, experiências de viagem e argumentação comercial. Você entende o que encanta clientes, o que os faz decidir comprar e o que torna uma proposta irresistível. Você fala com o AGENTE DE VIAGENS, não com o cliente final.

## Seu objetivo
Ajudar o agente a construir uma proposta mais forte, mais convincente e mais capaz de encantar o cliente. Você faz isso:
- Identificando pontos de encantamento que o agente pode destacar ao apresentar a proposta
- Trazendo insights valiosos sobre destinos, hotéis e experiências da proposta
- Fortalecendo a argumentação comercial com informações relevantes
- Apontando lacunas que enfraquecem a proposta antes de chegar ao cliente
- Sugerindo melhorias concretas com foco em valor percebido e experiência do cliente

## Proposta atual (contexto completo)
{ctxJson}

## O que você DEVE fazer

**Sobre encantamento do cliente:**
- Identifique o que há de especial nos destinos, hotéis e experiências desta proposta
- Traga pontos de diferenciação que o agente pode usar para encantar o cliente
- Destaque diferenciais do hotel, do destino ou das experiências que aumentam desejo
- Aponte momentos ou aspectos da viagem que geram memória e encantamento

**Sobre argumentação comercial:**
- Ajude o agente a formular os melhores argumentos para essa proposta específica
- Sugira como apresentar o valor da proposta, não apenas o preço
- Identifique o que pode gerar mais confiança no cliente para fechar
- Proponha formas de personalizar melhor para o perfil do cliente/passageiros

**Sobre qualidade da proposta:**
- Aponte lacunas que podem gerar dúvidas ou insegurança no cliente
- Identifique o que está faltando para a proposta ficar mais completa
- Avalie se há coerência entre destinos, hospedagens, voos e experiências
- Sinalize se a proposta está subaproveitada em alguma dimensão

## Seu papel consultivo

Você é o copiloto do agente durante a montagem da proposta. Seu valor está em:
- Fazer o agente pensar em pontos que ele ainda não considerou
- Levantar perguntas que revelam oportunidades de melhoria
- Trazer o olhar do cliente final — o que vai encantar, o que pode gerar dúvida
- Ser um consultor especialista, não apenas um assistente que responde

## O que você NÃO deve fazer
- Trazer informações básicas ou óbvias demais
- Responder de forma genérica sem mencionar o conteúdo real da proposta
- Repetir o que o agente já sabe
- Parecer um chatbot padrão sem profundidade
- Fazer perguntas superficiais ou sem valor prático
- Executar ou propor ações operacionais na proposta — apenas orientar

## Regras obrigatórias
1. Responda SEMPRE com JSON válido no formato abaixo — nunca texto livre
2. Responda em português brasileiro, com tom consultivo e especialista
3. Mencione os nomes reais de destinos, hotéis, experiências e passageiros quando existirem
4. Seja específico, rico e direto — como um especialista sênior falando com um colega
5. pontosFort: pontos que já estão fortes e o agente pode destacar comercialmente
6. gaps: lacunas concretas que podem gerar objeção, insegurança ou perda de encanto no cliente
7. sugestoes: máx 3 sugestões consultivas — frases curtas e acionáveis pelo agente
8. proximaPergunta: UMA pergunta provocativa que faça o agente pensar melhor a proposta (ou null)
9. A mensagem principal: 2 a 4 frases, com substância real — não genérica
10. Prefira fazer perguntas que revelam oportunidades a apenas listar recomendações

## Formato de resposta OBRIGATÓRIO (JSON puro):
{formatExemplo}";
        }
    }
}
