using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SistemaUsuarios.Models.Dto;
using UglyToad.PdfPig;

namespace SistemaUsuarios.Services
{
    public class ImportacaoIAService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        private static readonly HashSet<string> _imageMimes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
        };

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ImportacaoIAService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["OpenAI:ApiKey"] ?? "";
        }

        public async Task<(TravelProposalDraft? Draft, string? Erro)> AnalisarAsync(List<IFormFile> arquivos)
        {
            if (!arquivos.Any())
                return (null, "Nenhum arquivo enviado.");

            var contentItems = new List<object>();
            var textoAcumulado = new StringBuilder();
            var imagensAnexadas = 0;

            foreach (var arquivo in arquivos.Take(10))
            {
                if (arquivo.Length > 20 * 1024 * 1024)
                {
                    textoAcumulado.AppendLine($"[Arquivo '{arquivo.FileName}' ignorado: tamanho excede 20 MB]");
                    continue;
                }

                var mime = arquivo.ContentType?.ToLowerInvariant() ?? "";
                var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

                using var ms = new MemoryStream();
                await arquivo.CopyToAsync(ms);
                ms.Position = 0;

                if (_imageMimes.Contains(mime) || ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp")
                {
                    var base64 = Convert.ToBase64String(ms.ToArray());
                    var mimeType = _imageMimes.Contains(mime) ? mime : "image/jpeg";
                    contentItems.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = $"data:{mimeType};base64,{base64}", detail = "high" }
                    });
                    imagensAnexadas++;
                }
                else if (ext == ".pdf" || mime == "application/pdf")
                {
                    var texto = ExtrairTextoPdf(ms);
                    if (!string.IsNullOrWhiteSpace(texto))
                        textoAcumulado.AppendLine($"=== Arquivo: {arquivo.FileName} ===\n{texto}\n");
                }
                else if (ext == ".docx" || mime.Contains("wordprocessingml") || mime.Contains("msword"))
                {
                    var texto = ExtrairTextoDocx(ms);
                    if (!string.IsNullOrWhiteSpace(texto))
                        textoAcumulado.AppendLine($"=== Arquivo: {arquivo.FileName} ===\n{texto}\n");
                }
                else if (ext is ".txt" or ".eml" or ".html" or ".htm" || mime.StartsWith("text/"))
                {
                    ms.Position = 0;
                    var texto = new StreamReader(ms, Encoding.UTF8).ReadToEnd();
                    textoAcumulado.AppendLine($"=== Arquivo: {arquivo.FileName} ===\n{texto}\n");
                }
                else
                {
                    textoAcumulado.AppendLine($"[Arquivo '{arquivo.FileName}' não suportado — ignorado]");
                }
            }

            if (textoAcumulado.Length > 0)
            {
                contentItems.Insert(0, new { type = "text", text = textoAcumulado.ToString() });
            }

            if (!contentItems.Any())
                return (null, "Nenhum conteúdo legível encontrado nos arquivos enviados.");

            if (contentItems.All(c => c is object o && o.GetType().GetProperty("type")?.GetValue(o)?.ToString() == "image_url"))
            {
                contentItems.Insert(0, new { type = "text", text = "Analise as imagens de documentos de viagem a seguir e extraia todas as informações estruturadas." });
            }

            return await ChamarGptAsync(contentItems);
        }

        private async Task<(TravelProposalDraft? Draft, string? Erro)> ChamarGptAsync(List<object> contentItems)
        {
            var systemPrompt = BuildSystemPrompt();

            var payload = new
            {
                model = "gpt-4o",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = contentItems }
                },
                response_format = new { type = "json_object" },
                max_tokens = 8192,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            HttpResponseMessage httpRes;
            try
            {
                httpRes = await _http.SendAsync(request);
            }
            catch (Exception ex)
            {
                return (null, $"Erro de conexão com a API: {ex.Message}");
            }

            var resJson = await httpRes.Content.ReadAsStringAsync();

            if (!httpRes.IsSuccessStatusCode)
                return (null, $"Erro na API OpenAI ({(int)httpRes.StatusCode}): {ExtrairMensagemErro(resJson)}");

            try
            {
                var doc = JsonDocument.Parse(resJson);
                var aiContent = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "{}";

                var draft = JsonSerializer.Deserialize<TravelProposalDraft>(aiContent, _jsonOpts);
                if (draft == null)
                    return (null, "Resposta inválida do modelo.");

                NormalizarIncluirDraft(draft);
                return (draft, null);
            }
            catch (Exception ex)
            {
                return (null, $"Erro ao interpretar resposta da IA: {ex.Message}");
            }
        }

        private static void NormalizarIncluirDraft(TravelProposalDraft d)
        {
            if (d.Proposta != null) d.Proposta.Incluir = true;
            foreach (var x in d.Passageiros) x.Incluir = true;
            foreach (var x in d.Voos) x.Incluir = true;
            foreach (var dest in d.Destinos)
            {
                dest.Incluir = true;
                foreach (var h in dest.Hospedagens) h.Incluir = true;
                foreach (var e in dest.Experiencias) e.Incluir = true;
                foreach (var t in dest.Transportes) t.Incluir = true;
            }
            foreach (var x in d.Seguros) x.Incluir = true;
        }

        private static string ExtrairTextoPdf(Stream stream)
        {
            try
            {
                using var pdf = PdfDocument.Open(stream);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    // Group words into lines by Y coordinate proximity (tolerance = 4 pts)
                    // Then sort lines top→bottom and words left→right
                    var wordList = page.GetWords().ToList();
                    if (!wordList.Any()) continue;

                    var lines = new List<(double yKey, List<(double x, string text)> words)>();
                    foreach (var word in wordList)
                    {
                        // PdfPig: Y increases upward; use Bottom as anchor
                        var y = Math.Round(word.BoundingBox.Bottom, 0);
                        var existing = lines.FirstOrDefault(l => Math.Abs(l.yKey - y) <= 4);
                        if (existing.words != null)
                        {
                            existing.words.Add((word.BoundingBox.Left, word.Text));
                        }
                        else
                        {
                            lines.Add((y, new List<(double, string)> { (word.BoundingBox.Left, word.Text) }));
                        }
                    }

                    // Sort lines descending by Y (top of page first in PDF coords)
                    foreach (var line in lines.OrderByDescending(l => l.yKey))
                    {
                        var lineText = string.Join(" ", line.words.OrderBy(w => w.x).Select(w => w.text));
                        sb.AppendLine(lineText);
                    }
                    sb.AppendLine(); // page break
                }
                return sb.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        private static string ExtrairTextoDocx(Stream stream)
        {
            try
            {
                using var doc = WordprocessingDocument.Open(stream, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return "";
                return string.Join("\n", body.Elements<Paragraph>()
                    .Select(p => p.InnerText)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
            }
            catch
            {
                return "";
            }
        }

        private static string ExtrairMensagemErro(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? json;
            }
            catch { return json.Length > 200 ? json[..200] : json; }
        }

        private static string BuildSystemPrompt() => @"Você é um especialista em leitura de documentos de viagem de operadoras e agências brasileiras (G7, CVC, Decolar, Infotera, Visual, Queensberry, etc.).
Sua função é ler o documento completo, compreender a viagem, extrair todos os dados e retornar um JSON que inclui tanto os dados estruturados quanto uma mensagem inicial explicando o que você encontrou.

══ REGRAS OBRIGATÓRIAS ══
1. Retorne APENAS JSON válido, sem nenhum texto fora do JSON
2. NUNCA invente informações — use null para campos ausentes
3. Datas: ISO ""yyyy-MM-dd"" para datas, ""yyyy-MM-ddTHH:mm:ss"" para data+hora
4. Coordenadas: forneça para cidades conhecidas; null se incerto

══ PADRÕES BRASILEIROS ══

DATAS PT-BR → ISO:
  jan=01 fev=02 mar=03 abr=04 mai=05 jun=06 jul=07 ago=08 set=09 out=10 nov=11 dez=12
  ""09 out 2026"" → ""2026-10-09""

MOEDA: ""R$ 1.234,56"" → 1234.56 (ponto=milhar, vírgula=decimal)

COMPANHIAS AÉREAS BRASILEIRAS:
  Azul=AD · LATAM=LA · Gol=G3
  ""Voo Operado por Azul"" + ""Voo: 2976"" → numeroVoo=""AD2976"", companhia=""Azul""

AEROPORTOS (IATA):
  GRU=São Paulo Guarulhos  CGH=Congonhas  VCP=Campinas  GIG=Rio Galeão  SDU=Santos Dumont
  BSB=Brasília  CNF=Confins/BH  CWB=Curitiba  POA=Porto Alegre  FLN=Florianópolis
  SSA=Salvador  REC=Recife  FOR=Fortaleza  NAT=Natal  MCZ=Maceió  BEL=Belém  MAO=Manaus

TIPO DE VOO:  Ida=voo de saída · Volta=retorno · Interno=conexão no destino

PENSÃO:
  Breakfast/Café da Manhã/BB → CafeDaManha
  Meia Pensão/HB → MeiaPensao
  Pensão Completa/FB → PensaoCompleta
  All Inclusive/AI → AllInclusive
  Sem refeição/RO → SemPensao

CATEGORIA: identifique pelo nome — ""Pousada X"" → Pousada, ""Resort Y"" → Resort, etc.
  Valores: Hotel · Pousada · Resort · HotelFazenda · AluguelCasa · Camping · Outros

PASSAGEIROS SEM NOME:
  ""2 Adultos 1 Criança (3 anos)"" → crie: ""Adulto 1"", ""Adulto 2"", ""Criança 1""
  Para criança com idade informada: dataNascimento = ano da viagem − idade (ex: 2026 − 3 = 2023-01-01)
  Criança → relacionamento=""Filho""

TRASLADO/TRANSFER → coloque em ""transportes"" (NUNCA em ""experiencias"")
PASSEIOS/EXCURSÕES/CITY TOUR → coloque em ""experiencias""
SEGURO VIAGEM → coloque em ""seguros""
NÚMERO DO ORÇAMENTO → proposta.observacoesGerais E hospedagem.reserva

══ MENSAGEM INICIAL ══
O campo ""mensagemInicial"" deve ser uma mensagem em primeira pessoa, como se você estivesse falando com o agente de viagens.
Exemplo:
  ""Analisei o orçamento 189312 da G7 Operadora referente a uma viagem para Gramado, Rio Grande do Sul, de 09 a 12 de outubro de 2026.\n\nEncontrei:\n✅ 3 passageiros (2 adultos + 1 criança de 3 anos)\n✅ 2 voos Azul (CWB→POA ida, POA→CWB volta)\n✅ 1 destino: Gramado com 1 hospedagem (Pousada Caminho dos Plátanos, 3 noites, café da manhã)\n✅ 1 traslado: Brocker Turismo (aeroporto POA ↔ Gramado)\n\nValor total: R$ 2.819,64\n\nPosso adicionar esses itens à sua proposta?""

O campo ""pendentes"" lista o que NÃO foi possível identificar (ex: nomes dos passageiros não especificados).
O campo ""alertas"" lista informações com baixa confiança.
O campo ""confiancaGeral"" é um número de 0 a 100 representando a confiança geral na extração.

══ ESTRUTURA JSON OBRIGATÓRIA ══
{
  ""mensagemInicial"": ""mensagem descritiva ao agente explicando o que foi encontrado, em PT-BR"",
  ""pendentes"": [""lista de itens que não foi possível identificar""],
  ""alertas"": [""lista de informações com baixa confiança""],
  ""confiancaGeral"": 85,
  ""proposta"": {
    ""titulo"": ""string descritivo (ex: Gramado · Out/2026) ou null"",
    ""observacoesGerais"": ""número do orçamento, operadora e condições relevantes ou null"",
    ""operadora"": ""nome da operadora ou null""
  },
  ""passageiros"": [
    {
      ""nome"": ""string obrigatório"",
      ""dataNascimento"": ""yyyy-MM-dd ou null"",
      ""genero"": ""Masculino ou Feminino ou null"",
      ""relacionamento"": ""Filho|Filha|Amigo|Amiga|Esposa|Marido|Sogra|Sogro|Cunhado|Cunhada|Neto|Neta|Pai|Mae|Irmao|Irma|Outro ou null"",
      ""observacoes"": ""string ou null""
    }
  ],
  ""voos"": [
    {
      ""numeroVoo"": ""string com prefixo IATA (ex: AD2976)"",
      ""tipoVoo"": ""Ida ou Volta ou Interno"",
      ""companhia"": ""nome da companhia"",
      ""classe"": ""Econômica ou Executiva ou Primeira ou null"",
      ""duracao"": ""ex: 1h15 ou null"",
      ""origem"": ""IATA 3 letras"",
      ""destino"": ""IATA 3 letras"",
      ""horarioSaida"": ""yyyy-MM-ddTHH:mm:ss ou null"",
      ""horarioChegada"": ""yyyy-MM-ddTHH:mm:ss ou null"",
      ""bagagemMaoPeso"": null,
      ""bagagemDespachadaPeso"": null
    }
  ],
  ""destinos"": [
    {
      ""nome"": ""nome do destino"",
      ""pais"": ""Brasil ou nome do país"",
      ""cidade"": ""nome da cidade"",
      ""dataChegada"": ""yyyy-MM-dd ou null"",
      ""dataSaida"": ""yyyy-MM-dd ou null"",
      ""latitude"": null,
      ""longitude"": null,
      ""descricao"": ""breve descrição turística do destino ou null"",
      ""hospedagens"": [
        {
          ""nome"": ""nome do hotel/pousada"",
          ""descricao"": ""tipo de quarto e regime ou null"",
          ""endereco"": ""endereço completo ou null"",
          ""latitude"": null,
          ""longitude"": null,
          ""checkIn"": ""yyyy-MM-dd ou null"",
          ""checkOut"": ""yyyy-MM-dd ou null"",
          ""categoria"": ""Hotel|Pousada|Resort|HotelFazenda|AluguelCasa|Camping|Outros"",
          ""tipoPensao"": ""SemPensao|CafeDaManha|MeiaPensao|PensaoCompleta|AllInclusive|Outros"",
          ""reserva"": ""código de reserva ou null"",
          ""observacoes"": ""observações adicionais ou null""
        }
      ],
      ""experiencias"": [
        {
          ""tipoPasseio"": ""nome do passeio ou excursão"",
          ""descricao"": ""string ou null"",
          ""valor"": null,
          ""dataInicio"": ""yyyy-MM-ddTHH:mm:ss ou null"",
          ""dataFim"": ""yyyy-MM-ddTHH:mm:ss ou null""
        }
      ],
      ""transportes"": [
        {
          ""titulo"": ""nome do traslado/transfer"",
          ""descricao"": ""empresa, trajeto e observações ou null"",
          ""valor"": null
        }
      ]
    }
  ],
  ""seguros"": [
    {
      ""titulo"": ""nome do seguro"",
      ""descricao"": ""string ou null"",
      ""valor"": null
    }
  ],
  ""valoresFinanceiros"": {
    ""valorTotal"": ""decimal sem formatação (ex: 2819.64) ou null"",
    ""observacoes"": ""condições de pagamento e parcelamento ou null""
  }
}";
    }
}
