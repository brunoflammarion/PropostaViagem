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

        public async Task<(ImportacaoPreviewDto? Preview, string? Erro)> AnalisarAsync(List<IFormFile> arquivos)
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
                contentItems.Insert(0, new { type = "text", text = "Analise as imagens de documentos de viagem a seguir e extraia todas as informações." });
            }

            return await ChamarGptAsync(contentItems);
        }

        private async Task<(ImportacaoPreviewDto? Preview, string? Erro)> ChamarGptAsync(List<object> contentItems)
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
                max_tokens = 4096,
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
            {
                return (null, $"Erro na API OpenAI ({(int)httpRes.StatusCode}): {ExtrairMensagemErro(resJson)}");
            }

            try
            {
                var doc = JsonDocument.Parse(resJson);
                var aiContent = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "{}";

                var preview = JsonSerializer.Deserialize<ImportacaoPreviewDto>(aiContent, _jsonOpts);
                if (preview == null)
                    return (null, "Resposta inválida do modelo.");

                // Garante Incluir=true em todos os itens retornados
                NormalizarIncluir(preview);

                return (preview, null);
            }
            catch (Exception ex)
            {
                return (null, $"Erro ao interpretar resposta da IA: {ex.Message}");
            }
        }

        private static void NormalizarIncluir(ImportacaoPreviewDto p)
        {
            if (p.Proposta != null) p.Proposta.Incluir = true;
            foreach (var x in p.Passageiros) x.Incluir = true;
            foreach (var x in p.Voos) x.Incluir = true;
            foreach (var d in p.Destinos)
            {
                d.Incluir = true;
                foreach (var h in d.Hospedagens) h.Incluir = true;
                foreach (var e in d.Experiencias) e.Incluir = true;
                foreach (var t in d.Transportes) t.Incluir = true;
            }
            foreach (var x in p.Seguros) x.Incluir = true;
        }

        private static string ExtrairTextoPdf(Stream stream)
        {
            try
            {
                using var pdf = PdfDocument.Open(stream);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var words = page.GetWords().Select(w => w.Text);
                    sb.AppendLine(string.Join(" ", words));
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

        private static string BuildSystemPrompt() => @"Você é um extrator especializado em documentos de viagem. Sua única função é analisar documentos (PDFs, imagens, e-mails, vouchers, confirmações de reserva) e retornar um JSON estruturado com TODAS as informações encontradas.

REGRAS OBRIGATÓRIAS:
1. Retorne APENAS JSON válido, sem nenhum texto adicional fora do JSON
2. NUNCA invente ou infira informações que não estejam explicitamente no documento
3. Use null para qualquer campo que não esteja presente no documento
4. Datas: use formato ISO ""yyyy-MM-dd"" para datas, ""yyyy-MM-ddTHH:mm:ss"" para data+hora
5. Coordenadas: forneça valores aproximados para cidades/locais conhecidos; use null se não tiver certeza
6. Todos os textos em português quando possível
7. Para tipo de voo: ""Ida"" = voo de ida, ""Volta"" = voo de retorno, ""Interno"" = doméstico/conexão
8. Para categoria de hospedagem: Hotel, Pousada, Resort, HotelFazenda, AluguelCasa, Camping, Outros
9. Para tipo de pensão: SemPensao, CafeDaManha, MeiaPensao, PensaoCompleta, AllInclusive, Outros

RETORNE ESTE JSON (preencha os campos encontrados, use null nos ausentes):
{
  ""proposta"": {
    ""titulo"": ""string ou null"",
    ""observacoesGerais"": ""string ou null"",
    ""operadora"": ""string ou null""
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
      ""numeroVoo"": ""string obrigatório (ex: LA3050)"",
      ""tipoVoo"": ""Ida ou Volta ou Interno"",
      ""companhia"": ""string obrigatório (ex: LATAM)"",
      ""classe"": ""string ou null"",
      ""duracao"": ""string ou null (ex: 2h30)"",
      ""origem"": ""código IATA ou nome da cidade"",
      ""destino"": ""código IATA ou nome da cidade"",
      ""horarioSaida"": ""yyyy-MM-ddTHH:mm:ss ou null"",
      ""horarioChegada"": ""yyyy-MM-ddTHH:mm:ss ou null"",
      ""bagagemMaoPeso"": ""número em kg ou null"",
      ""bagagemDespachadaPeso"": ""número em kg ou null""
    }
  ],
  ""destinos"": [
    {
      ""nome"": ""string obrigatório"",
      ""pais"": ""string ou null"",
      ""cidade"": ""string ou null"",
      ""dataChegada"": ""yyyy-MM-dd ou null"",
      ""dataSaida"": ""yyyy-MM-dd ou null"",
      ""latitude"": ""número decimal ou null"",
      ""longitude"": ""número decimal ou null"",
      ""descricao"": ""string ou null"",
      ""hospedagens"": [
        {
          ""nome"": ""string obrigatório"",
          ""descricao"": ""string ou null"",
          ""endereco"": ""string ou null"",
          ""latitude"": ""número decimal ou null"",
          ""longitude"": ""número decimal ou null"",
          ""checkIn"": ""yyyy-MM-dd ou null"",
          ""checkOut"": ""yyyy-MM-dd ou null"",
          ""categoria"": ""Hotel|Pousada|Resort|HotelFazenda|AluguelCasa|Camping|Outros"",
          ""tipoPensao"": ""SemPensao|CafeDaManha|MeiaPensao|PensaoCompleta|AllInclusive|Outros"",
          ""reserva"": ""número/código de reserva ou null"",
          ""observacoes"": ""string ou null""
        }
      ],
      ""experiencias"": [
        {
          ""tipoPasseio"": ""string obrigatório"",
          ""descricao"": ""string ou null"",
          ""valor"": ""número decimal ou null"",
          ""dataInicio"": ""yyyy-MM-ddTHH:mm:ss ou null"",
          ""dataFim"": ""yyyy-MM-ddTHH:mm:ss ou null""
        }
      ],
      ""transportes"": [
        {
          ""titulo"": ""string obrigatório"",
          ""descricao"": ""string ou null"",
          ""valor"": ""número decimal ou null""
        }
      ]
    }
  ],
  ""seguros"": [
    {
      ""titulo"": ""string obrigatório"",
      ""descricao"": ""string ou null"",
      ""valor"": ""número decimal ou null""
    }
  ],
  ""valoresFinanceiros"": {
    ""valorTotal"": ""número decimal ou null"",
    ""observacoes"": ""string com resumo financeiro ou null""
  }
}";
    }
}
