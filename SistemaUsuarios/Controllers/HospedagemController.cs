using SistemaUsuarios.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Controllers
{
    public class HospedagemController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly BlobStorageService _blob;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HospedagemController> _logger;

        // Máximo 2 chamadas simultâneas ao Google Places — proteção contra avalanche
        private static readonly SemaphoreSlim _autocompleteThrottle = new(2, 2);

        public HospedagemController(ApplicationDbContext context, IConfiguration configuration, BlobStorageService blob,
            IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<HospedagemController> logger)
        {
            _context = context;
            _blob = blob;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        private bool UsuarioLogado() => HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() => Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        private IActionResult RedirectToEditar(Guid propostaId)
        {
            TempData["ActiveTab"] = "destinos";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        // GET: Hospedagem/BuscarSugestoesHotel?termo=marriott&cidade=Paris
        [HttpGet]
        public async Task<IActionResult> BuscarSugestoesHotel(string termo, string? cidade, CancellationToken cancellationToken)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { error = "Sessão expirada." });

            if (string.IsNullOrWhiteSpace(termo) || termo.Length < 4)
                return BadRequest(new { error = "Mínimo 4 caracteres." });

            if (termo.Length > 200)
                return BadRequest(new { error = "Termo muito longo." });

            var externalEnabled = _configuration.GetValue<bool>("FeatureFlags:HotelAutocompleteExternalSearch", true);
            if (!externalEnabled)
                return Json(new { predictions = Array.Empty<object>() });

            var normalizedTermo = termo.Trim().ToLowerInvariant();
            var normalizedCidade = cidade?.Trim().ToLowerInvariant() ?? "";
            var cacheKey = $"hotel-ac:{normalizedTermo}:{normalizedCidade}";

            if (_cache.TryGetValue(cacheKey, out string? cached))
            {
                _logger.LogDebug("Hotel autocomplete cache HIT {Key}", cacheKey);
                return Content(cached!, "application/json");
            }

            if (!await _autocompleteThrottle.WaitAsync(0, cancellationToken))
            {
                _logger.LogWarning("Hotel autocomplete throttled — concorrência máxima atingida");
                return StatusCode(429, new { error = "Muitas buscas simultâneas. Aguarde um momento." });
            }

            try
            {
                var apiKey = _configuration["GoogleApiKey"];
                var input = string.IsNullOrWhiteSpace(cidade) ? termo : $"{termo} {cidade}";
                var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json" +
                          $"?input={Uri.EscapeDataString(input)}&types=lodging&language=pt-BR&key={apiKey}";

                var http = _httpClientFactory.CreateClient("GooglePlaces");
                var response = await http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Google Places retornou {Status} para termo {Termo}", (int)response.StatusCode, termo);
                    return StatusCode((int)response.StatusCode, new { error = "Erro ao consultar Places API." });
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                try
                {
                    var parsed = JObject.Parse(content);
                    if (parsed["predictions"] is JArray predictions && predictions.Count > 10)
                    {
                        parsed["predictions"] = new JArray(predictions.Take(10));
                        content = parsed.ToString(Formatting.None);
                    }
                }
                catch (JsonReaderException) { /* devolve o conteúdo original se parse falhar */ }

                _cache.Set(cacheKey, content, TimeSpan.FromMinutes(5));

                return Content(content, "application/json");
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { error = "Requisição cancelada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sugestões de hotel para {Termo}", termo);
                return StatusCode(500, new { error = "Erro interno. Tente novamente." });
            }
            finally
            {
                _autocompleteThrottle.Release();
            }
        }

        // GET: Hospedagem/BuscarDetalhesHotel?placeId=ChIJ...
        [HttpGet]
        public async Task<IActionResult> BuscarDetalhesHotel(string placeId, CancellationToken cancellationToken)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { error = "Sessão expirada." });

            if (string.IsNullOrEmpty(placeId) || placeId.Length > 300)
                return BadRequest(new { error = "placeId inválido." });

            var apiKey = _configuration["GoogleApiKey"];
            var fields = "name,formatted_address,geometry,rating,website,formatted_phone_number,types";
            var url = $"https://maps.googleapis.com/maps/api/place/details/json" +
                      $"?place_id={placeId}&fields={fields}&language=pt-BR&key={apiKey}";

            try
            {
                var http = _httpClientFactory.CreateClient("GooglePlaces");
                var response = await http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { error = "Erro ao consultar detalhes." });

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                JObject obj;
                try { obj = JObject.Parse(json); }
                catch (JsonReaderException ex)
                {
                    _logger.LogError(ex, "JSON inválido recebido do Google Places Details para placeId {PlaceId}", placeId);
                    return StatusCode(500, new { error = "Resposta inválida da API." });
                }

                var status = obj["status"]?.ToString();
                if (status != "OK")
                {
                    _logger.LogWarning("Google Places Details retornou status {Status} para placeId {PlaceId}", status, placeId);
                    return BadRequest(new { error = "Local não encontrado." });
                }

                var result = obj["result"];
                if (result == null)
                    return BadRequest(new { error = "Detalhes não encontrados." });

                var nome = result["name"]?.ToString();
                var endereco = result["formatted_address"]?.ToString();
                var telefone = result["formatted_phone_number"]?.ToString();
                var website = result["website"]?.ToString();
                var rating = result["rating"]?.ToObject<double?>();
                double? latitude = null, longitude = null;

                var location = result["geometry"]?["location"];
                if (location != null)
                {
                    latitude = location["lat"]?.ToObject<double?>();
                    longitude = location["lng"]?.ToObject<double?>();
                }

                var types = result["types"]?.ToObject<string[]>() ?? Array.Empty<string>();
                var categoria = InferirCategoria(types);

                return Json(new { nome, endereco, telefone, website, rating, latitude, longitude, categoria });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { error = "Requisição cancelada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar detalhes do hotel {PlaceId}", placeId);
                return StatusCode(500, new { error = "Erro interno. Tente novamente." });
            }
        }

        // POST: Hospedagem/GerarDescricaoHospedagem (acionado apenas pelo clique explícito do usuário)
        [HttpPost]
        public async Task<IActionResult> GerarDescricaoHospedagem(string nome, string? endereco, string? cidade, CancellationToken cancellationToken)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { error = "Sessão expirada." });

            if (string.IsNullOrWhiteSpace(nome))
                return BadRequest(new { error = "Nome da hospedagem é obrigatório." });

            var aiEnabled = _configuration.GetValue<bool>("FeatureFlags:HotelAiEnrichment", true);
            if (!aiEnabled)
                return StatusCode(503, new { error = "Enriquecimento por IA temporariamente desativado." });

            var prompt = $@"Você é um especialista em turismo e hotéis. Gere informações completas e comerciais sobre a hospedagem ""{nome}""{(string.IsNullOrWhiteSpace(cidade) ? "" : $" localizada em {cidade}")}.

Responda SOMENTE com um JSON válido, sem texto fora do objeto, no seguinte formato:

{{
  ""descricao"": ""Descrição comercial e atraente da hospedagem em até 3 parágrafos. Destaque diferenciais, localização, estilo e por que é uma boa escolha. NUNCA use aspas duplas dentro dos valores."",
  ""comodidades"": [""lista"", ""de"", ""comodidades"", ""principais""],
  ""dicasCheckIn"": ""Dicas práticas sobre check-in, check-out, horários e procedimentos."",
  ""observacoesGerais"": ""Observações importantes para os hospedes: estacionamento, pets, cafe da manha, piscina, academia, etc.""
}}";

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new { error = "Chave da API da OpenAI não configurada." });

            try
            {
                var httpClient = _httpClientFactory.CreateClient("OpenAI");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    temperature = 0.5,
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um especialista em turismo e hospitalidade. Responda sempre com JSON válido." },
                        new { role = "user", content = prompt }
                    }
                };

                var jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var erro = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("OpenAI retornou {Status} para hospedagem {Nome}: {Erro}", (int)response.StatusCode, nome, erro);
                    return StatusCode((int)response.StatusCode, new { error = "Erro ao consultar IA." });
                }

                var respostaJson = await response.Content.ReadAsStringAsync(cancellationToken);
                JObject resultado;
                try { resultado = JObject.Parse(respostaJson); }
                catch (JsonReaderException ex)
                {
                    _logger.LogError(ex, "JSON inválido recebido da OpenAI para hospedagem {Nome}", nome);
                    return StatusCode(500, new { error = "Resposta inválida da IA." });
                }

                var jsonTexto = resultado["choices"]?[0]?["message"]?["content"]?.ToString();
                if (string.IsNullOrWhiteSpace(jsonTexto))
                    return StatusCode(500, new { error = "Resposta da OpenAI vazia." });

                var jsonLimpo = ExtrairJson(jsonTexto);
                try
                {
                    var parsed = JObject.Parse(jsonLimpo);
                    return Content(parsed.ToString(Formatting.None), "application/json");
                }
                catch (JsonReaderException ex)
                {
                    _logger.LogError(ex, "JSON inválido retornado pela IA para hospedagem {Nome}: {Raw}", nome, jsonTexto);
                    return StatusCode(500, new { error = "JSON inválido retornado pela IA." });
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { error = "Requisição cancelada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição para hospedagem {Nome}", nome);
                return StatusCode(500, new { error = "Erro interno. Tente novamente." });
            }
        }

        private static int InferirCategoria(string[] types)
        {
            if (types.Contains("lodging"))
            {
                if (types.Contains("resort_hotel") || types.Contains("resort")) return (int)CategoriaHospedagem.Resort;
                if (types.Contains("hotel")) return (int)CategoriaHospedagem.Hotel;
                if (types.Contains("campground")) return (int)CategoriaHospedagem.Camping;
            }
            return (int)CategoriaHospedagem.Hotel;
        }

        private static string ExtrairJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "{}";
            raw = raw.Trim();
            var fence = new Regex(@"^```json\s*([\s\S]*?)\s*```$", RegexOptions.IgnoreCase);
            var m = fence.Match(raw);
            if (m.Success) raw = m.Groups[1].Value;
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
                return raw.Substring(start, end - start + 1);
            return raw;
        }

        // POST: Hospedagem/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid destinoId,
            string nome,
            string? descricao,
            string? endereco,
            string? latitude,
            string? longitude,
            DateTime? checkIn,
            DateTime? checkOut,
            CategoriaHospedagem categoria,
            TipoPensao tipoPensao,
            string? reserva,
            string? observacoes,
            string? comodidadesJson = null,
            IFormFile[]? fotos = null)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == destinoId && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

            if (destino == null)
            {
                TempData["Erro"] = "Destino não encontrado";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome da hospedagem é obrigatório";
                return RedirectToEditar(destino.PropostaId);
            }

            var maxOrdem = await _context.Hospedagens
                .Where(h => h.DestinoId == destinoId)
                .MaxAsync(h => (int?)h.Ordem) ?? 0;

            var hospedagem = new Hospedagem
            {
                DestinoId = destinoId,
                Nome = nome.Trim(),
                Descricao = descricao?.Trim(),
                Endereco = endereco?.Trim(),
                Latitude = ParseCoord(latitude, -90, 90),
                Longitude = ParseCoord(longitude, -180, 180),
                CheckIn = checkIn,
                CheckOut = checkOut,
                Categoria = categoria,
                TipoPensao = tipoPensao,
                Reserva = reserva?.Trim(),
                Observacoes = observacoes?.Trim(),
                Ordem = maxOrdem + 1
            };

            _context.Hospedagens.Add(hospedagem);
            await _context.SaveChangesAsync();

            // Salvar comodidades vindas do modal (geradas pela IA ou adicionadas manualmente)
            if (!string.IsNullOrWhiteSpace(comodidadesJson))
            {
                var nomes = comodidadesJson
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int ordemComod = 1;
                foreach (var nomeComod in nomes)
                {
                    _context.HospedagemComodidades.Add(new HospedagemComodidade
                    {
                        HospedagemId = hospedagem.Id,
                        Nome = nomeComod,
                        Ordem = ordemComod++
                    });
                }
                await _context.SaveChangesAsync();
            }

            // Salvar fotos enviadas junto com o formulário de criação
            var fotosValidas = (fotos ?? Array.Empty<IFormFile>())
                .Where(f => f != null && f.Length > 0).ToList();

            if (fotosValidas.Any())
            {
                var errosFoto = new List<string>();
                for (int i = 0; i < fotosValidas.Count; i++)
                {
                    try
                    {
                        var caminho = await SalvarFotoHospedagemAsync(fotosValidas[i]);
                        _context.HospedagemFotos.Add(new HospedagemFoto
                        {
                            HospedagemId = hospedagem.Id,
                            CaminhoFoto  = caminho,
                            Principal    = i == 0,   // primeira foto marcada como principal
                            Ordem        = i + 1
                        });
                    }
                    catch (InvalidOperationException ex)
                    {
                        errosFoto.Add($"{fotosValidas[i].FileName}: {ex.Message}");
                    }
                }
                await _context.SaveChangesAsync();

                if (errosFoto.Any())
                    TempData["Aviso"] = $"Hospedagem criada, mas {errosFoto.Count} foto(s) não puderam ser salvas: {string.Join(" | ", errosFoto)}";
                else
                    TempData["Sucesso"] = $"Hospedagem '{nome}' adicionada com {fotosValidas.Count} foto(s)!";
            }
            else
            {
                TempData["Sucesso"] = $"Hospedagem '{nome}' adicionada com sucesso!";
            }

            return RedirectToEditar(destino.PropostaId);
        }

        // POST: Hospedagem/AdicionarComodidade
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarComodidade(Guid hospedagemId, string nome)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Destino).ThenInclude(d => d.Proposta)
                .Include(h => h.Comodidades)
                .FirstOrDefaultAsync(h => h.Id == hospedagemId && (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null)
            {
                TempData["Erro"] = "Hospedagem não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var nomeTrimmed = nome?.Trim();
            if (string.IsNullOrWhiteSpace(nomeTrimmed))
            {
                TempData["Erro"] = "Nome da comodidade é obrigatório";
                return RedirectToEditar(hospedagem.Destino.PropostaId);
            }

            if (hospedagem.Comodidades.Any(c => c.Nome.Equals(nomeTrimmed, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Aviso"] = $"Comodidade '{nomeTrimmed}' já existe nesta hospedagem.";
                return RedirectToEditar(hospedagem.Destino.PropostaId);
            }

            var maxOrdem = hospedagem.Comodidades.Any() ? hospedagem.Comodidades.Max(c => c.Ordem) : 0;
            _context.HospedagemComodidades.Add(new HospedagemComodidade
            {
                HospedagemId = hospedagemId,
                Nome = nomeTrimmed,
                Ordem = maxOrdem + 1
            });

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Comodidade '{nomeTrimmed}' adicionada!";
            return RedirectToEditar(hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/ExcluirComodidade
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirComodidade(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var comodidade = await _context.HospedagemComodidades
                .Include(c => c.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(c => c.Id == id && (isMaster ? c.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : c.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (comodidade == null)
            {
                TempData["Erro"] = "Comodidade não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = comodidade.Hospedagem.Destino.PropostaId;
            _context.HospedagemComodidades.Remove(comodidade);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Comodidade removida.";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        private static string? SanitizeDescricao(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Regex.Replace(value.Trim(), @"<(?!\/?b\b|br\s*\/?\s*)[^>]*>", "", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Parses a lat or lng string using InvariantCulture and validates range.
        /// Accepts dot or comma as decimal separator for robustness.
        /// Returns null if value is missing, unparseable, or out of valid range.
        /// </summary>
        private static double? ParseCoord(string? raw, double min, double max)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            // Normalise: replace comma with dot to handle locale-rendered values
            var normalised = raw.Trim().Replace(',', '.');
            if (!double.TryParse(normalised, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out var val))
                return null;
            return (val >= min && val <= max) ? val : null;
        }

        public async Task<IActionResult> Editar(
            Guid id,
            string nome,
            string? descricao,
            string? endereco,
            string? latitude,
            string? longitude,
            DateTime? checkIn,
            DateTime? checkOut,
            CategoriaHospedagem categoria,
            TipoPensao tipoPensao,
            string? reserva)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(h => h.Id == id && (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null)
            {
                TempData["Erro"] = "Hospedagem não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome da hospedagem é obrigatório";
                return RedirectToEditar(hospedagem.Destino.PropostaId);
            }

            hospedagem.Nome = nome.Trim();
            hospedagem.Descricao = SanitizeDescricao(descricao);
            hospedagem.Endereco = endereco?.Trim();
            hospedagem.Latitude = ParseCoord(latitude, -90, 90);
            hospedagem.Longitude = ParseCoord(longitude, -180, 180);
            hospedagem.CheckIn = checkIn;
            hospedagem.CheckOut = checkOut;
            hospedagem.Categoria = categoria;
            hospedagem.TipoPensao = tipoPensao;
            hospedagem.Reserva = reserva?.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Hospedagem '{nome}' atualizada com sucesso!";
            return RedirectToEditar(hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Destino)
                    .ThenInclude(d => d.Proposta)
                .Include(h => h.Fotos)
                .Include(h => h.Acomodacoes)
                    .ThenInclude(a => a.Fotos)
                .FirstOrDefaultAsync(h => h.Id == id && (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null)
            {
                TempData["Erro"] = "Hospedagem não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = hospedagem.Destino.PropostaId;
            var nomeHospedagem = hospedagem.Nome;

            // Remover arquivos físicos das fotos do hotel
            foreach (var foto in hospedagem.Fotos)
                ExcluirArquivoFisico(foto.CaminhoFoto);

            // Remover arquivos físicos das fotos das acomodações
            foreach (var acomodacao in hospedagem.Acomodacoes)
            {
                foreach (var foto in acomodacao.Fotos)
                    ExcluirArquivoFisico(foto.CaminhoFoto);
            }

            _context.Hospedagens.Remove(hospedagem);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Hospedagem '{nomeHospedagem}' excluída com sucesso!";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/AdicionarFotoHospedagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarFotoHospedagem(Guid hospedagemId, IFormFile[] fotos, bool principal = false)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var hospedagem = await _context.Hospedagens
                .Include(h => h.Fotos)
                .Include(h => h.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(h => h.Id == hospedagemId &&
                    (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null)
            {
                TempData["Erro"] = "Hospedagem não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var fotosValidas = (fotos ?? Array.Empty<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (!fotosValidas.Any())
            {
                TempData["Erro"] = "Selecione ao menos uma foto";
                return RedirectToEditar(hospedagem.Destino.PropostaId);
            }

            if (principal)
            {
                foreach (var f in hospedagem.Fotos)
                    f.Principal = false;
            }

            var maxOrdem = hospedagem.Fotos.Any() ? hospedagem.Fotos.Max(f => f.Ordem) : 0;
            var erros = new List<string>();
            int adicionadas = 0;

            for (int i = 0; i < fotosValidas.Count; i++)
            {
                try
                {
                    var caminho = await SalvarFotoHospedagemAsync(fotosValidas[i]);
                    _context.HospedagemFotos.Add(new HospedagemFoto
                    {
                        HospedagemId = hospedagemId,
                        CaminhoFoto = caminho,
                        Principal = principal && i == 0,
                        Ordem = maxOrdem + i + 1
                    });
                    adicionadas++;
                }
                catch (InvalidOperationException ex)
                {
                    erros.Add($"{fotosValidas[i].FileName}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            TempData[erros.Any() ? "Erro" : "Sucesso"] = erros.Any()
                ? string.Join(" | ", erros)
                : (adicionadas == 1 ? "Foto do hotel adicionada!" : $"{adicionadas} fotos do hotel adicionadas!");

            return RedirectToEditar(hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/ExcluirFotoHospedagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirFotoHospedagem(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var foto = await _context.HospedagemFotos
                .Include(f => f.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id &&
                    (isMaster ? f.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : f.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (foto == null)
            {
                TempData["Erro"] = "Foto não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = foto.Hospedagem.Destino.PropostaId;
            var hospedagemId = foto.HospedagemId;

            ExcluirArquivoFisico(foto.CaminhoFoto);
            _context.HospedagemFotos.Remove(foto);
            await _context.SaveChangesAsync();

            // Reordenar fotos restantes
            var restantes = await _context.HospedagemFotos
                .Where(f => f.HospedagemId == hospedagemId)
                .OrderBy(f => f.Ordem)
                .ToListAsync();
            for (int i = 0; i < restantes.Count; i++)
                restantes[i].Ordem = i + 1;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Foto do hotel excluída.";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/ReordenarFotosHospedagem
        [HttpPost]
        public async Task<IActionResult> ReordenarFotosHospedagem([FromBody] ReordenarFotosHospedagemRequest request)
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var hospedagem = await _context.Hospedagens
                .Include(h => h.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(h => h.Id == request.HospedagemId &&
                    (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null) return NotFound();

            for (int i = 0; i < request.FotosIds.Count; i++)
            {
                var foto = await _context.HospedagemFotos
                    .FirstOrDefaultAsync(f => f.Id == request.FotosIds[i] && f.HospedagemId == request.HospedagemId);
                if (foto != null)
                    foto.Ordem = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST: Hospedagem/AdicionarComodidadeAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarComodidadeAcomodacao(Guid acomodacaoId, string nome)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var acomodacao = await _context.Acomodacoes
                .Include(a => a.Comodidades)
                .Include(a => a.Hospedagem)
                    .ThenInclude(h => h.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == acomodacaoId &&
                    (isMaster ? a.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : a.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (acomodacao == null)
            {
                TempData["Erro"] = "Acomodação não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var nomeTrimmed = nome?.Trim();
            if (string.IsNullOrWhiteSpace(nomeTrimmed))
            {
                TempData["Erro"] = "Nome da comodidade é obrigatório";
                return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
            }

            if (acomodacao.Comodidades.Any(c => c.Nome.Equals(nomeTrimmed, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Aviso"] = $"Comodidade '{nomeTrimmed}' já existe nesta acomodação.";
                return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
            }

            var maxOrdem = acomodacao.Comodidades.Any() ? acomodacao.Comodidades.Max(c => c.Ordem) : 0;
            _context.AcomodacaoComodidades.Add(new AcomodacaoComodidade
            {
                AcomodacaoId = acomodacaoId,
                Nome = nomeTrimmed,
                Ordem = maxOrdem + 1
            });

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Comodidade '{nomeTrimmed}' adicionada à acomodação!";
            return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/ExcluirComodidadeAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirComodidadeAcomodacao(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var comodidade = await _context.AcomodacaoComodidades
                .Include(c => c.Acomodacao)
                    .ThenInclude(a => a.Hospedagem)
                        .ThenInclude(h => h.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(c => c.Id == id &&
                    (isMaster ? c.Acomodacao.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : c.Acomodacao.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (comodidade == null)
            {
                TempData["Erro"] = "Comodidade não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = comodidade.Acomodacao.Hospedagem.Destino.PropostaId;
            _context.AcomodacaoComodidades.Remove(comodidade);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Comodidade da acomodação removida.";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/AdicionarAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAcomodacao(Guid hospedagemId, string nome, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(h => h.Id == hospedagemId && (isMaster ? h.Destino.Proposta.UsuarioMasterId == usuarioId : h.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (hospedagem == null)
            {
                TempData["Erro"] = "Hospedagem não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome da acomodação é obrigatório";
                return RedirectToEditar(hospedagem.Destino.PropostaId);
            }

            var maxOrdem = await _context.Acomodacoes
                .Where(a => a.HospedagemId == hospedagemId)
                .MaxAsync(a => (int?)a.Ordem) ?? 0;

            var acomodacao = new Acomodacao
            {
                HospedagemId = hospedagemId,
                Nome = nome.Trim(),
                Descricao = descricao?.Trim(),
                Ordem = maxOrdem + 1
            };

            _context.Acomodacoes.Add(acomodacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Acomodação '{nome}' adicionada com sucesso!";
            return RedirectToEditar(hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/EditarAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarAcomodacao(Guid id, string nome, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var acomodacao = await _context.Acomodacoes
                .Include(a => a.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : a.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (acomodacao == null)
            {
                TempData["Erro"] = "Acomodação não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome da acomodação é obrigatório";
                return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
            }

            acomodacao.Nome = nome.Trim();
            acomodacao.Descricao = descricao?.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Acomodação atualizada com sucesso!";
            return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/ExcluirAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirAcomodacao(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var acomodacao = await _context.Acomodacoes
                .Include(a => a.Fotos)
                .Include(a => a.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : a.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (acomodacao == null)
            {
                TempData["Erro"] = "Acomodação não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = acomodacao.Hospedagem.Destino.PropostaId;

            foreach (var foto in acomodacao.Fotos)
            {
                ExcluirArquivoFisico(foto.CaminhoFoto);
            }

            _context.Acomodacoes.Remove(acomodacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Acomodação excluída com sucesso!";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/AdicionarFotoAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarFotoAcomodacao(Guid acomodacaoId, IFormFile[] fotos, string? descricao, bool principal = false)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var acomodacao = await _context.Acomodacoes
                .Include(a => a.Fotos)
                .Include(a => a.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == acomodacaoId && (isMaster ? a.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : a.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (acomodacao == null)
            {
                TempData["Erro"] = "Acomodação não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var fotosValidas = (fotos ?? Array.Empty<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (!fotosValidas.Any())
            {
                TempData["Erro"] = "Selecione ao menos uma foto para fazer upload";
                return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
            }

            try
            {
                if (principal)
                {
                    foreach (var f in acomodacao.Fotos)
                        f.Principal = false;
                }

                var maxOrdem = acomodacao.Fotos.Any() ? acomodacao.Fotos.Max(f => f.Ordem) : 0;
                var erros = new List<string>();
                int adicionadas = 0;

                for (int i = 0; i < fotosValidas.Count; i++)
                {
                    try
                    {
                        var caminho = await SalvarFotoAsync(fotosValidas[i]);
                        _context.AcomodacaoFotos.Add(new AcomodacaoFoto
                        {
                            AcomodacaoId = acomodacaoId,
                            CaminhoFoto = caminho,
                            Descricao = descricao?.Trim(),
                            Principal = principal && i == 0,
                            Ordem = maxOrdem + i + 1
                        });
                        adicionadas++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        erros.Add($"{fotosValidas[i].FileName}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                if (erros.Any())
                    TempData["Erro"] = string.Join(" | ", erros);
                else
                    TempData["Sucesso"] = adicionadas == 1
                        ? "Foto adicionada com sucesso!"
                        : $"{adicionadas} fotos adicionadas com sucesso!";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Erro"] = ex.Message;
            }

            return RedirectToEditar(acomodacao.Hospedagem.Destino.PropostaId);
        }

        // POST: Hospedagem/ExcluirFotoAcomodacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirFotoAcomodacao(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var foto = await _context.AcomodacaoFotos
                .Include(f => f.Acomodacao)
                    .ThenInclude(a => a.Hospedagem)
                        .ThenInclude(h => h.Destino)
                            .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id && (isMaster ? f.Acomodacao.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : f.Acomodacao.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (foto == null)
            {
                TempData["Erro"] = "Foto não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = foto.Acomodacao.Hospedagem.Destino.PropostaId;

            ExcluirArquivoFisico(foto.CaminhoFoto);
            _context.AcomodacaoFotos.Remove(foto);
            await _context.SaveChangesAsync();

            // Reordenar fotos restantes
            var fotosRestantes = await _context.AcomodacaoFotos
                .Where(f => f.AcomodacaoId == foto.AcomodacaoId)
                .OrderBy(f => f.Ordem)
                .ToListAsync();

            for (int i = 0; i < fotosRestantes.Count; i++)
                fotosRestantes[i].Ordem = i + 1;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Foto excluída com sucesso!";
            return RedirectToEditar(propostaId);
        }

        // POST: Hospedagem/ReordenarFotosAcomodacao
        [HttpPost]
        public async Task<IActionResult> ReordenarFotosAcomodacao([FromBody] ReordenarFotosAcomodacaoRequest request)
        {
            if (!UsuarioLogado()) return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var acomodacao = await _context.Acomodacoes
                .Include(a => a.Hospedagem)
                    .ThenInclude(h => h.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == request.AcomodacaoId &&
                    (isMaster ? a.Hospedagem.Destino.Proposta.UsuarioMasterId == usuarioId : a.Hospedagem.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (acomodacao == null) return NotFound();

            if (request.FotosIds == null || !request.FotosIds.Any())
                return BadRequest("Lista de IDs de fotos não pode estar vazia.");

            if (request.FotosIds.Distinct().Count() != request.FotosIds.Count)
                return BadRequest("Lista contém IDs duplicados.");

            var fotos = await _context.AcomodacaoFotos
                .Where(f => f.AcomodacaoId == request.AcomodacaoId)
                .ToListAsync();

            var fotosIds = fotos.Select(f => f.Id).ToHashSet();
            if (request.FotosIds.Any(id => !fotosIds.Contains(id)))
                return BadRequest("Um ou mais IDs não pertencem a esta acomodação.");

            // Duas fases para evitar violação do índice único {AcomodacaoId, Ordem}
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Fase 1: ordens temporárias negativas
                for (int i = 0; i < fotos.Count; i++)
                    fotos[i].Ordem = -1000 - i;

                await _context.SaveChangesAsync();

                // Fase 2: ordem final
                for (int i = 0; i < request.FotosIds.Count; i++)
                {
                    var foto = fotos.First(f => f.Id == request.FotosIds[i]);
                    foto.Ordem = i + 1;
                }

                var naoListadas = fotos.Where(f => !request.FotosIds.Contains(f.Id)).ToList();
                for (int i = 0; i < naoListadas.Count; i++)
                    naoListadas[i].Ordem = request.FotosIds.Count + i + 1;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Erro ao reordenar fotos." });
            }
        }

        private Task<string> SalvarFotoAsync(IFormFile foto)
            => _blob.SalvarAsync(foto, "acomodacoes");

        private Task<string> SalvarFotoHospedagemAsync(IFormFile foto)
            => _blob.SalvarAsync(foto, "hospedagens");

        private void ExcluirArquivoFisico(string? url)
            => _ = _blob.DeletarAsync(url);
    }

    public class ReordenarFotosAcomodacaoRequest
    {
        public Guid AcomodacaoId { get; set; }
        public List<Guid> FotosIds { get; set; } = new();
    }

    public class ReordenarFotosHospedagemRequest
    {
        public Guid HospedagemId { get; set; }
        public List<Guid> FotosIds { get; set; } = new();
    }
}
