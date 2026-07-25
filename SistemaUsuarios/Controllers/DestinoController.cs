using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SistemaUsuarios.Controllers;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Moq;
using SistemaUsuarios.Migrations;
using SistemaUsuarios.Services;
using Destino = SistemaUsuarios.Models.Destino;



namespace SistemaUsuarios.Controllers
{
    public class DestinoController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IAiGatewayService _aiGateway;

        public DestinoController(IConfiguration configuration, ApplicationDbContext context, IAiGatewayService aiGateway)
        {
            _configuration = configuration;
            _context = context;
            _aiGateway = aiGateway;
        }

        private bool UsuarioLogado() => HttpContext.Session.GetString("UsuarioId") != null;
        private Guid ObterUsuarioLogadoId() => Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);
        private bool SessaoIsMaster() => HttpContext.Session.GetString("TipoUsuario") != "Associado";
        private Guid ObterAgenciaId()
        {
            if (SessaoIsMaster()) return ObterUsuarioLogadoId();
            var s = HttpContext.Session.GetString("UsuarioMasterId");
            return string.IsNullOrEmpty(s) ? ObterUsuarioLogadoId() : Guid.Parse(s);
        }
        [HttpPost]
        public async Task<IActionResult> GerarDescricao2(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome) )
                return BadRequest("Cidade e mês são obrigatórios.");

            var prompt = $@"
Você é um redator especializado em turismo e expert no destino {nome}.

Sua tarefa é gerar um conteúdo completo, com foco comercial, para agentes de viagem que estão montando uma proposta para clientes. A resposta deve ser estruturada em um JSON com os seguintes campos:

1. **descricao**  
Crie uma descrição curta e inspiradora da cidade de {nome}, destacando seus atrativos turísticos, clima, estilo de vida e por que ela é um ótimo destino para visitar. Use um tom empolgante, leve e comercial. Limite a resposta a 3 parágrafos curtos.

2. **atracoes**  
Liste as 5 principais atrações turísticas de {nome}. Para cada uma, retorne:
- nome
- descricao (máx. 2 frases)
- tipoPublico (ex: famílias, casais, aventureiros)

3. **gastronomia**  
Liste os 5 restaurantes mais recomendados em {nome}. Para cada um, retorne:
- nome
- tipoCulinaria
- pratoDestaque
- perfilIdeal (ex: casal, família, luxo, econômico)

4. **informacoesPraticas**  
Liste informações úteis para quem vai a {nome} pela primeira vez. Inclua:
- melhor epoca para visitar
- clima médio
- meios de transporte
- documentos exigidos
- dicas gerais

5. **malaViagem**  
Liste recomendações do que levar na mala para uma viagem para {nome}. Considere:
- roupas adequadas ao clima
- itens obrigatórios (vacinas, documentos)
- acessórios úteis para o destino

6. **cuidados**  
Informe os principais cuidados que o viajante deve ter. Inclua:
- segurança
- regiões a evitar (se houver)
- vacinas recomendadas
- golpes comuns
- orientações práticas

Importante: Responda em formato JSON estruturado com as chaves correspondentes aos tópicos acima. Evite comentários fora do JSON. Use linguagem natural, clara e objetiva.";

            if (!UsuarioLogado()) return Unauthorized();

            var gatewayResult = await _aiGateway.ExecutarAsync(new AiGatewayRequest
            {
                AgenciaId = ObterAgenciaId(),
                UsuarioId = ObterUsuarioLogadoId(),
                Funcionalidade = "GerarDescricaoDestino2",
                Modelo = "gpt-4o-mini",
                Payload = new
                {
                    model = "gpt-4o-mini",
                    temperature = 0.5,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um redator de viagens profissional. Responda sempre com JSON válido." },
                        new { role = "user", content = prompt }
                    }
                }
            });

            if (!gatewayResult.Sucesso)
                return StatusCode(500, $"Erro ao gerar descrição: {gatewayResult.MensagemErro}");

            var jsonTexto = gatewayResult.Conteudo ?? "";
            if (string.IsNullOrWhiteSpace(jsonTexto))
                return StatusCode(500, "Resposta da OpenAI está vazia.");

            try
            {
                var jsonExtraido = ExtrairJsonDeMarkdown(jsonTexto);
                return Content(jsonExtraido, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao interpretar JSON da OpenAI: {ex.Message}");
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> GerarDescricao(string nome, Guid? propostaId = null, DateTime? dataChegada = null, DateTime? dataSaida = null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return BadRequest("Nome do destino é obrigatório.");

            if (!UsuarioLogado()) return Unauthorized();

            var contextoBlock = "";
            if (propostaId.HasValue)
            {
                var usuarioId = ObterUsuarioLogadoId();
                var isMaster = SessaoIsMaster();
                var proposta = await _context.Propostas
                    .Include(p => p.PassageirosProposta)
                    .Include(p => p.Voos)
                    .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                        .ThenInclude(d => d.Hospedagens)
                    .Include(p => p.Destinos)
                        .ThenInclude(d => d.Experiencias)
                    .Include(p => p.Destinos)
                        .ThenInclude(d => d.Transportes)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(p => p.Id == propostaId.Value
                        && (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

                if (proposta != null)
                    contextoBlock = BuildContextoPropostaBlock(proposta, nome, dataChegada, dataSaida);
            }

            var prompt = BuildPromptGerarDescricao(nome, dataChegada, dataSaida, contextoBlock);

            var gatewayResult = await _aiGateway.ExecutarAsync(new AiGatewayRequest
            {
                AgenciaId = ObterAgenciaId(),
                UsuarioId = ObterUsuarioLogadoId(),
                Funcionalidade = "GerarDescricaoDestino",
                Modelo = "gpt-4o-mini",
                Payload = new
                {
                    model = "gpt-4o-mini",
                    temperature = 0.5,
                    max_tokens = 2800,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um consultor sênior de viagens apoiando agentes de viagem brasileiros. Responda sempre com JSON válido. NUNCA use aspas duplas dentro dos valores de string — use aspas simples." },
                        new { role = "user", content = prompt }
                    }
                }
            });

            if (!gatewayResult.Sucesso)
                return StatusCode(500, $"Erro ao gerar descrição: {gatewayResult.MensagemErro}");

            var jsonTexto = gatewayResult.Conteudo ?? "";
            if (string.IsNullOrWhiteSpace(jsonTexto))
                return StatusCode(500, "Resposta da OpenAI está vazia.");

            var jsonExtraido = ExtrairJsonDeMarkdown(jsonTexto);
            try
            {
                var parsed = JObject.Parse(jsonExtraido);
                return Content(parsed.ToString(Formatting.None), "application/json");
            }
            catch (JsonReaderException ex)
            {
                return StatusCode(500, $"JSON inválido retornado pela IA: {ex.Message}");
            }
        }

        private static string BuildContextoPropostaBlock(Proposta proposta, string nomeDestino, DateTime? dataChegadaDest, DateTime? dataSaidaDest)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Proposta: {proposta.Titulo}");
            if (proposta.DataInicio.HasValue && proposta.DataFim.HasValue)
            {
                var totalDias = (int)(proposta.DataFim.Value - proposta.DataInicio.Value).TotalDays + 1;
                sb.AppendLine($"Período da viagem: {proposta.DataInicio:dd/MM/yyyy} a {proposta.DataFim:dd/MM/yyyy} ({totalDias} dias)");
                sb.AppendLine($"Mês de partida: {proposta.DataInicio:MMMM} de {proposta.DataInicio:yyyy}");
            }

            var passageiros = proposta.PassageirosProposta?.OrderBy(p => p.Ordem).ToList() ?? new List<PassageiroProposta>();
            int totalPax = passageiros.Count > 0 ? passageiros.Count : proposta.NumeroPassageiros + proposta.NumeroCriancas;
            int criancas = passageiros.Count > 0
                ? passageiros.Count(p => p.IdadeCalculada.HasValue && p.IdadeCalculada < 12)
                : proposta.NumeroCriancas;
            int adultos = totalPax - criancas;

            sb.AppendLine($"Passageiros: {totalPax} ({adultos} adulto(s), {criancas} criança(s))");

            if (passageiros.Any())
            {
                var paxDesc = passageiros.Select(p =>
                {
                    var partes = new List<string>();
                    if (p.IdadeCalculada.HasValue) partes.Add($"{p.IdadeCalculada} anos");
                    if (p.Relacionamento.HasValue) partes.Add(p.Relacionamento.ToString()!);
                    return partes.Any() ? $"{p.Nome} ({string.Join(", ", partes)})" : p.Nome;
                });
                sb.AppendLine($"Perfil dos passageiros: {string.Join(" | ", paxDesc)}");
            }

            if (dataChegadaDest.HasValue || dataSaidaDest.HasValue)
            {
                int? diasDest = dataChegadaDest.HasValue && dataSaidaDest.HasValue
                    ? (int)(dataSaidaDest.Value - dataChegadaDest.Value).TotalDays : null;
                var periodoDest = dataChegadaDest.HasValue && dataSaidaDest.HasValue
                    ? $"{dataChegadaDest:dd/MM/yyyy} a {dataSaidaDest:dd/MM/yyyy} ({diasDest} dia(s))"
                    : dataChegadaDest.HasValue ? $"chegada em {dataChegadaDest:dd/MM/yyyy}" : "";
                sb.AppendLine($"Período em {nomeDestino}: {periodoDest}");
            }

            var outrosDestinos = proposta.Destinos?
                .Where(d => !string.Equals(d.Nome, nomeDestino, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Ordem).ToList() ?? new List<Destino>();

            if (outrosDestinos.Any())
            {
                var outros = outrosDestinos.Select(d =>
                {
                    var s = d.Nome;
                    if (d.DataChegada.HasValue && d.DataSaida.HasValue)
                        s += $" ({(int)(d.DataSaida.Value - d.DataChegada.Value).TotalDays}d)";
                    return s;
                });
                sb.AppendLine($"Outros destinos da viagem: {string.Join(" → ", outros)}");
            }

            var thisDestino = proposta.Destinos?.FirstOrDefault(d => string.Equals(d.Nome, nomeDestino, StringComparison.OrdinalIgnoreCase));
            if (thisDestino != null)
            {
                if (thisDestino.Hospedagens?.Any() == true)
                    sb.AppendLine($"Hospedagens já cadastradas: {string.Join(", ", thisDestino.Hospedagens.Select(h => $"{h.Nome} ({h.Categoria})"))}");
                if (thisDestino.Experiencias?.Any() == true)
                    sb.AppendLine($"Experiências já cadastradas: {string.Join(", ", thisDestino.Experiencias.Select(e => e.TipoPasseio ?? e.Descricao ?? "Experiência"))}");
                if (thisDestino.Transportes?.Any() == true)
                    sb.AppendLine($"Transportes já cadastrados: {string.Join(", ", thisDestino.Transportes.Select(t => t.Titulo))}");
            }

            if (proposta.Voos?.Any() == true)
            {
                var voos = proposta.Voos.OrderBy(v => v.HorarioSaida).Select(v =>
                {
                    var s = $"{v.TipoVoo}: {v.Origem}→{v.Destino}";
                    if (v.HorarioSaida.HasValue) s += $" ({v.HorarioSaida:dd/MM})";
                    return s;
                });
                sb.AppendLine($"Voos cadastrados: {string.Join(" | ", voos)}");
            }

            if (!string.IsNullOrWhiteSpace(proposta.ObservacoesGerais))
                sb.AppendLine($"Observações do cliente: {proposta.ObservacoesGerais}");

            return sb.ToString();
        }

        private static string BuildPromptGerarDescricao(string nome, DateTime? dataChegada, DateTime? dataSaida, string contextoBlock)
        {
            var temContexto = !string.IsNullOrWhiteSpace(contextoBlock);
            var sb = new StringBuilder();

            if (temContexto)
            {
                sb.AppendLine("## CONTEXTO DA PROPOSTA");
                sb.AppendLine(contextoBlock);
            }

            sb.AppendLine($"## DESTINO: {nome}");
            sb.AppendLine();

            sb.AppendLine("## SUA TAREFA");
            sb.AppendLine("Gere um JSON com dois blocos de conteúdo:");
            sb.AppendLine("1. Conteúdo para o cliente (descricao, atracoes, gastronomia, informacoesPraticas, malaViagem, cuidados, pratosTipicos)");
            sb.AppendLine("2. Insights consultivos para o agente (insightsAgente)");
            sb.AppendLine();

            if (temContexto)
            {
                sb.AppendLine("## REGRAS PARA insightsAgente");
                sb.AppendLine("Você é um consultor sênior de viagens. Escreva para o AGENTE, não para o cliente.");
                sb.AppendLine("Analise o contexto da proposta (passageiros, período, itens já cadastrados, observações) e gere entre 3 e 7 insights.");
                sb.AppendLine();
                sb.AppendLine("Antes de incluir qualquer insight, avalie internamente se ele passa em TODOS estes critérios:");
                sb.AppendLine("a) É específico para esta proposta (perfil + destino + período concreto)?");
                sb.AppendLine("b) Pode alterar uma decisão real (hotel, voo, itinerário, experiência)?");
                sb.AppendLine("c) Ajuda a evitar risco, erro ou frustração — OU aumenta valor percebido?");
                sb.AppendLine("d) É acionável pelo agente agora?");
                sb.AppendLine("e) NÃO é genérico ('reserve com antecedência', 'leve roupas confortáveis', 'experimente a culinária local') sem motivo concreto?");
                sb.AppendLine();
                sb.AppendLine("Não mostre a avaliação interna. Inclua apenas os insights aprovados.");
                sb.AppendLine("Se existirem apenas 2 pontos que passem, retorne 2 — não complete com insights fracos.");
                sb.AppendLine();
                sb.AppendLine("Ordene por prioridade:");
                sb.AppendLine("1. Riscos críticos (documentação, saúde, sazonalidade adversa, restrições para o perfil)");
                sb.AppendLine("2. Decisões de logística e preço");
                sb.AppendLine("3. Adequação ao perfil dos passageiros");
                sb.AppendLine("4. Oportunidades comerciais");
                sb.AppendLine("5. Dicas complementares de alto valor");
                sb.AppendLine();
                sb.AppendLine("Quando o insight depender de dado variável (visto, vacina, horário de atração, taxa local), use na acaoRecomendada: 'Confirme a regra vigente antes de emitir.'");
            }
            else
            {
                sb.AppendLine("## REGRAS PARA insightsAgente");
                sb.AppendLine("Sem contexto de proposta. Gere insights gerais de alto valor para agentes que trabalham com este destino.");
                sb.AppendLine("Foque em: riscos críticos de documentação ou saúde, impacto real da sazonalidade, dicas de logística que afetam custo, adequação a diferentes perfis.");
            }

            sb.AppendLine();
            sb.AppendLine("## REGRAS GERAIS");
            sb.AppendLine("- Responda SOMENTE com JSON válido, sem nenhum texto fora do objeto");
            sb.AppendLine("- NUNCA use aspas duplas dentro de valores de string");
            sb.AppendLine("- Sem comentários dentro do JSON");
            sb.AppendLine("- Português natural, claro e profissional");
            sb.AppendLine("- Não invente dados ausentes no contexto");
            sb.AppendLine("- atracoes e gastronomia: entre 3 e 5 itens cada");
            sb.AppendLine("- pratosTipicos: entre 2 e 4 itens");
            sb.AppendLine();

            sb.Append(@"{
  ""descricao"": ""Apresentação inspiradora do destino adequada ao perfil da viagem (2-3 parágrafos). Voltada ao cliente final."",
  ""atracoes"": [
    {
      ""nome"": ""Nome oficial da atração"",
      ""descricao"": ""Resumo objetivo em até 2 frases"",
      ""tipoPublico"": ""Ex: famílias, casais, aventureiros"",
      ""ganchoComercial"": ""Por que incluir nesta proposta""
    }
  ],
  ""gastronomia"": [
    {
      ""nome"": ""Nome do restaurante ou experiência gastronômica"",
      ""tipoCulinaria"": ""Ex: italiana, frutos do mar"",
      ""pratoDestaque"": ""Principal prato recomendado"",
      ""perfilIdeal"": ""Ex: casal, família, luxo"",
      ""ganchoComercial"": ""Motivo comercial para indicar""
    }
  ],
  ""informacoesPraticas"": {
    ""melhorEpoca"": ""Melhor período com justificativa"",
    ""climaMedio"": ""Clima típico e médias de temperatura"",
    ""transportes"": ""Meios de transporte disponíveis"",
    ""documentos"": ""Documentos obrigatórios para entrada"",
    ""dicasGerais"": ""Dicas práticas específicas""
  },
  ""malaViagem"": {
    ""roupas"": ""Sugestões para o clima local"",
    ""itensObrigatorios"": ""Vacinas, documentos, seguros necessários"",
    ""acessoriosUteis"": ""Itens úteis para este destino""
  },
  ""cuidados"": {
    ""seguranca"": ""Recomendações de segurança específicas"",
    ""regioesEvitar"": ""Áreas a evitar, se houver"",
    ""vacinas"": ""Vacinas recomendadas"",
    ""golpesComuns"": ""Situações típicas a evitar"",
    ""orientacoesPraticas"": ""Orientações úteis""
  },
  ""pratosTipicos"": [
    {
      ""nome"": ""Nome do prato típico"",
      ""descricao"": ""Breve explicação"",
      ""ocasiao"": ""Quando/como é consumido"",
      ""ganchoComercial"": ""Motivo para citar na proposta""
    }
  ],
  ""insightsAgente"": [
    {
      ""titulo"": ""Título curto e direto"",
      ""explicacao"": ""Explicação objetiva e específica para esta proposta"",
      ""impactoProposta"": ""Como isso afeta logística, preço ou experiência"",
      ""acaoRecomendada"": ""O que o agente deve fazer ou verificar"",
      ""prioridade"": ""Alta""
    }
  ]
}");

            return sb.ToString();
        }

private static string ExtrairJsonDeMarkdown(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return "{}";
    raw = raw.Trim();

    // remove cercas ```json ... ```
    var fence = new Regex("^```json\\s*([\\s\\S]*?)\\s*```$", RegexOptions.IgnoreCase);
    var m = fence.Match(raw);
    if (m.Success) raw = m.Groups[1].Value;

    // pega apenas o primeiro objeto balanceado
    var start = raw.IndexOf('{');
    var end = raw.LastIndexOf('}');
    if (start >= 0 && end > start)
        return raw.Substring(start, end - start + 1);

    return raw; // deixa o caller tentar parsear
}
        private static string ExtrairJsonDeMarkdown2(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "{}";

            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
                return raw.Substring(start, end - start + 1);

            return raw;
        }


        [HttpGet]
        public async Task<IActionResult> BuscarDetalhes(string placeId){
            if (string.IsNullOrEmpty(placeId))
                return BadRequest("placeId inválido.");

            var apiKey = _configuration["Google:ApiKey"] ?? _configuration["GoogleApiKey"];
            var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key={apiKey}";

            using var http = new HttpClient();
            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Erro ao consultar detalhes.");

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            var status = obj["status"]?.ToString();
            if (status != "OK")
                return BadRequest("Google Places retornou erro: " + status);

            var result = obj["result"];
            if (result == null)
                return BadRequest("Detalhes não encontrados.");

            string cidade = null, pais = null;
            double? latitude = null, longitude = null;

            var componentes = result["address_components"];
            if (componentes != null)
            {
                foreach (var comp in componentes)
                {
                    var tipos = comp["types"]?.ToObject<string[]>();
                    if (tipos != null)
                    {
                        if (tipos.Contains("locality") || tipos.Contains("administrative_area_level_2"))
                            cidade = comp["long_name"]?.ToString();

                        if (tipos.Contains("country"))
                            pais = comp["long_name"]?.ToString();
                    }
                }
            }

    var geometry = result["geometry"];
    if (geometry != null)
    {
        var location = geometry["location"];
        if (location != null)
        {
            latitude = location["lat"]?.ToObject<double>();
            longitude = location["lng"]?.ToObject<double>();
        }
    }

    return Json(new { cidade, pais, latitude, longitude });
}


        [HttpGet]
        public async Task<IActionResult> BuscarSugestoes(string termo)
        {
            if (string.IsNullOrWhiteSpace(termo))
                return BadRequest("Termo obrigatório");

            var httpClient = new HttpClient();
            var apiKey = _configuration["GoogleApiKey"];
            var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(termo)}&types=(cities)&language=pt-BR&key={apiKey}";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Erro ao consultar Google Places");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        private IActionResult RedirectToEditar(Guid propostaId, Guid? destinoAberto = null, string? focoId = null)
        {
            TempData["ActiveTab"] = "destinos";
            if (destinoAberto.HasValue)
                TempData["DestinoAberto"] = destinoAberto.Value.ToString();
            if (focoId != null)
                TempData["FocoElementId"] = focoId;
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        // GET: Destino/Gerenciar/PropostaId
        public async Task<IActionResult> Gerenciar(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            // Buscar proposta com destinos, fotos e hospedagens
            var proposta = await _context.Propostas
                .Include(p => p.Cliente)
                .Include(p => p.PassageirosProposta)
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens.OrderBy(h => h.Ordem))
                        .ThenInclude(h => h.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes.OrderBy(a => a.Ordem))
                            .ThenInclude(a => a.Fotos.OrderBy(f => f.Ordem))
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == propostaId && (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            return View(proposta);
        }

        // POST: Destino/AdicionarDestino
        [HttpPost]
        public async Task<IActionResult> AdicionarDestino(Guid propostaId
                                                    , string nome
                                                    , string? pais
                                                    , string? cidade
                                                    , DateTime? dataChegada
                                                    , DateTime? dataSaida
                                                    , string? descricao

                                                    , string? AtracoesLLM
                                                    , string? GastronomiaLLM
                                                    , string? InformacoesPraticasLLM
                                                    , string? MalaViagemLLM
                                                    , string? CuidadosLLM
                                                    , string? pratosTipicosLLM
                                                    , string? insightesAgenteLLM

                                                    , double? latitude
                                                    , double? longitude)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            // Verificar proposta
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound();

            if (string.IsNullOrEmpty(nome))
            {
                TempData["Erro"] = "Nome do destino é obrigatório";
                return RedirectToEditar(propostaId);
            }

            // Validação de datas
            if (dataChegada.HasValue && dataSaida.HasValue && dataChegada > dataSaida)
            {
                TempData["Erro"] = "Data de saída deve ser posterior à data de chegada";
                return RedirectToEditar(propostaId);
            }

            // Determinar próxima ordem
            var maxOrdem = await _context.Destinos
                .Where(d => d.PropostaId == propostaId)
                .MaxAsync(d => (int?)d.Ordem) ?? 0;

            var destino = new Destino
            {
                Id = Guid.NewGuid()
                ,PropostaId = propostaId
                ,Nome = nome.Trim()
                ,Pais = pais?.Trim()
                ,Cidade = cidade?.Trim()
                ,DataChegada = dataChegada
                ,DataSaida = dataSaida
                ,Descricao = descricao?.Trim()

                ,AtracoesLLM = AtracoesLLM
                ,GastronomiaLLM = GastronomiaLLM
                ,InformacoesPraticasLLM = InformacoesPraticasLLM
                ,MalaViagemLLM = MalaViagemLLM
                ,CuidadosLLM = CuidadosLLM
                ,PratosTipicosLLM = pratosTipicosLLM
                ,InsightesAgenteLLM = insightesAgenteLLM

                ,Ordem = maxOrdem + 1
                ,DataCriacao = DateTime.Now
                ,Latitude = NormalizarCoordenada(latitude)
                ,Longitude = NormalizarCoordenada(longitude)
                ,Localizacao = latitude.HasValue && longitude.HasValue ? new Point(NormalizarCoordenada(longitude.Value), NormalizarCoordenada(latitude.Value)) { SRID = 4326 } : null
            };

            _context.Destinos.Add(destino);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nome}' adicionado. Agora você pode incluir fotos, hospedagens e experiências.";
            TempData["NovoDestinoId"] = destino.Id.ToString();
            return RedirectToEditar(propostaId);
        }
        private double NormalizarCoordenada(object valor)
        {
            if (valor == null) return 0;

            var valorStr = valor.ToString().Trim();

            // Detecta e separa o sinal
            var isNegativo = valorStr.StartsWith("-");
            var parteNumerica = isNegativo ? valorStr.Substring(1) : valorStr;

            
            // Garante exatamente 9 dígitos numéricos
            if (parteNumerica.Length < 9)
                parteNumerica = parteNumerica.PadRight(9, '0'); // completa com zeros à direita
            // Mantém até 7 dígitos da parte numérica
            else if (parteNumerica.Length > 9)
                parteNumerica = parteNumerica.Substring(0, 9); // corta se for maior que 9

            var truncado = isNegativo ? "-" + parteNumerica : parteNumerica;

            // Converte para double e divide por 1e7
            if (double.TryParse(truncado, out double bruto))
                return bruto / 1e7;

            return 0;
        }

        // POST: Destino/EditarDestino
        [HttpPost]
        public async Task<IActionResult> EditarDestino(Guid id, string nome, string? pais, string? cidade, DateTime? dataChegada, DateTime? dataSaida, string? descricao,
            string? AtracoesLLM, string? GastronomiaLLM, string? InformacoesPraticasLLM, string? MalaViagemLLM, string? CuidadosLLM, string? PratosTipicosLLM, string? insightesAgenteLLM)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

            if (destino == null)
                return NotFound();

            if (string.IsNullOrEmpty(nome))
            {
                TempData["Erro"] = "Nome do destino é obrigatório";
                return RedirectToEditar(destino.PropostaId);
            }

            // Validação de datas
            if (dataChegada.HasValue && dataSaida.HasValue && dataChegada > dataSaida)
            {
                TempData["Erro"] = "Data de saída deve ser posterior à data de chegada";
                return RedirectToEditar(destino.PropostaId);
            }

            destino.Nome = nome.Trim();
            destino.Pais = pais?.Trim();
            destino.Cidade = cidade?.Trim();
            destino.DataChegada = dataChegada;
            destino.DataSaida = dataSaida;
            destino.Descricao = descricao?.Trim();

            if (AtracoesLLM != null) destino.AtracoesLLM = AtracoesLLM;
            if (GastronomiaLLM != null) destino.GastronomiaLLM = GastronomiaLLM;
            if (InformacoesPraticasLLM != null) destino.InformacoesPraticasLLM = InformacoesPraticasLLM;
            if (MalaViagemLLM != null) destino.MalaViagemLLM = MalaViagemLLM;
            if (CuidadosLLM != null) destino.CuidadosLLM = CuidadosLLM;
            if (PratosTipicosLLM != null) destino.PratosTipicosLLM = PratosTipicosLLM;
            if (insightesAgenteLLM != null) destino.InsightesAgenteLLM = insightesAgenteLLM;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nome}' editado com sucesso!";
            return RedirectToEditar(destino.PropostaId, destino.Id);
        }

        // POST: Destino/ExcluirDestino
        [HttpPost]
        public async Task<IActionResult> ExcluirDestino(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .Include(d => d.Fotos)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

            if (destino == null)
                return NotFound();

            var propostaId = destino.PropostaId;
            var nomeDestino = destino.Nome;

            // Remover arquivos físicos das fotos
            foreach (var foto in destino.Fotos)
            {
                if (!string.IsNullOrEmpty(foto.CaminhoFoto))
                {
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.CaminhoFoto.TrimStart('/'));
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        System.IO.File.Delete(caminhoCompleto);
                    }
                }
            }

            _context.Destinos.Remove(destino);
            await _context.SaveChangesAsync();

            // Reordenar destinos restantes
            var destinos = await _context.Destinos
                .Where(d => d.PropostaId == propostaId)
                .OrderBy(d => d.Ordem)
                .ToListAsync();

            for (int i = 0; i < destinos.Count; i++)
            {
                destinos[i].Ordem = i + 1;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nomeDestino}' excluído com sucesso.";
            return RedirectToEditar(propostaId);
        }

        // POST: Destino/ReordenarDestinos
        [HttpPost]
        public async Task<IActionResult> ReordenarDestinos([FromBody] ReordenarDestinosRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            // Verificar se a proposta pertence ao usuário
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == request.PropostaId && (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound();

            // Atualizar ordem dos destinos
            for (int i = 0; i < request.DestinosIds.Count; i++)
            {
                var destino = await _context.Destinos
                    .FirstOrDefaultAsync(d => d.Id == request.DestinosIds[i] && d.PropostaId == request.PropostaId);

                if (destino != null)
                {
                    destino.Ordem = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }

    // DTO para reordenação
    public class ReordenarDestinosRequest
    {
        public Guid PropostaId { get; set; }
        public List<Guid> DestinosIds { get; set; } = new();
    }
    public class ChatCompletionRequest
    {
        public string model { get; set; } = "gpt-4";
        public List<Message> messages { get; set; } = new();
        public double temperature { get; set; } = 0.7;
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}