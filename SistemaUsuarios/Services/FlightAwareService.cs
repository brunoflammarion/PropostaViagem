using System.Text.Json;

namespace SistemaUsuarios.Services
{
    public class FlightInfoResult
    {
        // Meta
        public string? CodigoBusca   { get; set; }
        public string? CodigoVoo     { get; set; }
        public string? IdentIata     { get; set; }
        public string? Erro          { get; set; }

        // Companhia
        public string? Companhia     { get; set; }
        public string? CompanhiaIata { get; set; }
        public string? CompanhiaIcao { get; set; }

        // Status / Aeronave
        public string? Status          { get; set; }
        public string? Codeshare       { get; set; }
        public string? ModeloAeronave  { get; set; }
        public DateTime? UltimaAtualizacao { get; set; }

        // Horários principais (calculados com prioridade)
        public DateTime? HorarioSaida   { get; set; }
        public DateTime? HorarioChegada { get; set; }
        public string?   Duracao        { get; set; }

        // Origem — aeroporto
        public string? Origem           { get; set; }
        public string? OrigemIata       { get; set; }
        public string? OrigemIcao       { get; set; }
        public string? OrigemNomeCurto  { get; set; }
        public string? OrigemCidade     { get; set; }
        public string? OrigemPais       { get; set; }
        public string? OrigemFuso       { get; set; }
        // Origem — horários detalhados
        public DateTime? SaidaLocalProgramada { get; set; }
        public DateTime? SaidaUtcProgramada   { get; set; }
        public DateTime? SaidaLocalRevisada   { get; set; }
        public DateTime? SaidaUtcRevisada     { get; set; }
        // Origem — operações
        public string? OrigemTerminal { get; set; }
        public string? OrigemPortao   { get; set; }
        public string? OrigemCheckIn  { get; set; }

        // Destino — aeroporto
        public string? Destino          { get; set; }
        public string? DestinoIata      { get; set; }
        public string? DestinoIcao      { get; set; }
        public string? DestinoNomeCurto { get; set; }
        public string? DestinoCidade    { get; set; }
        public string? DestinoPais      { get; set; }
        public string? DestinoFuso      { get; set; }
        // Destino — horários detalhados
        public DateTime? ChegadaLocalProgramada { get; set; }
        public DateTime? ChegadaUtcProgramada   { get; set; }
        public DateTime? ChegadaLocalRevisada   { get; set; }
        public DateTime? ChegadaUtcRevisada     { get; set; }
        public DateTime? ChegadaLocalPrevista   { get; set; }
        public DateTime? ChegadaUtcPrevista     { get; set; }
    }

    public interface IFlightAwareService
    {
        Task<FlightInfoResult> ConsultarVooAsync(string numeroVoo);
    }

    public class FlightAwareService : IFlightAwareService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FlightAwareService> _logger;
        private readonly string _apiKey;

        // URL base completa — usada diretamente para evitar o bug de resolução
        // de URLs relativas com barra inicial no HttpClient.
        // (Leading slash + BaseAddress sem trailing slash faz o HttpClient
        //  descartar o segmento de path do BaseAddress, causando 404.)
        private const string AeroApiBase = "https://aeroapi.flightaware.com/aeroapi";

        public FlightAwareService(HttpClient httpClient, IConfiguration config, ILogger<FlightAwareService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = config["FlightAware:ApiKey"] ?? string.Empty;
            // Não configuramos BaseAddress nem DefaultRequestHeaders aqui —
            // o header x-apikey é adicionado por-request para segurança e clareza.
        }

        public async Task<FlightInfoResult> ConsultarVooAsync(string numeroVoo)
        {
            if (string.IsNullOrWhiteSpace(numeroVoo))
                return new FlightInfoResult { Erro = "Número do voo não informado." };

            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "CONFIGURE_VIA_USER_SECRETS")
                return new FlightInfoResult { Erro = "Chave da API FlightAware não configurada. Configure via User Secrets." };

            // Normalizar ident: maiúsculo, sem espaços ("LA 1234" → "LA1234")
            var ident = numeroVoo.Trim().ToUpperInvariant().Replace(" ", "");

            // URL absoluta explícita — evita qualquer ambiguidade de resolução relativa
            var url = $"{AeroApiBase}/flights/{Uri.EscapeDataString(ident)}";

            _logger.LogDebug("FlightAware: GET {Url}", url);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                // Autenticação obrigatória: header x-apikey (AeroAPI padrão)
                request.Headers.Add("x-apikey", _apiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("FlightAware retornou {Status} para {Ident}: {Body}",
                        (int)response.StatusCode, ident, errorBody);

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound =>
                            new FlightInfoResult { Erro = $"Voo {ident} não encontrado na FlightAware." },
                        System.Net.HttpStatusCode.Unauthorized =>
                            new FlightInfoResult { Erro = "Autenticação FlightAware inválida. Verifique a chave de API." },
                        System.Net.HttpStatusCode.TooManyRequests =>
                            new FlightInfoResult { Erro = "Limite de consultas FlightAware atingido. Tente novamente em instantes." },
                        _ =>
                            new FlightInfoResult { Erro = $"Erro ao consultar FlightAware ({(int)response.StatusCode})." }
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("FlightAware resposta para {Ident} ({Bytes} bytes)", ident, json.Length);

                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("flights", out var flights)
                    || flights.GetArrayLength() == 0)
                    return new FlightInfoResult { Erro = $"Nenhum voo encontrado para {ident}." };

                // Usar o primeiro voo da lista (mais recente/relevante)
                var voo = flights[0];
                var resultado = new FlightInfoResult();

                // ── Ident IATA ───────────────────────────────────────────────────────────
                if (voo.TryGetProperty("ident_iata", out var identIata)
                    && identIata.ValueKind == JsonValueKind.String)
                    resultado.IdentIata = identIata.GetString();

                // ── Companhia ────────────────────────────────────────────────────────────
                // Preferir operator_iata (código IATA de 2 letras, ex: "LA", "G3", "AD")
                // antes de operator (código ICAO de 3 letras, ex: "LAN", "GLO", "AZU")
                if (voo.TryGetProperty("operator_iata", out var opIata)
                    && opIata.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(opIata.GetString()))
                    resultado.Companhia = opIata.GetString();
                else if (voo.TryGetProperty("operator", out var op)
                    && op.ValueKind == JsonValueKind.String)
                    resultado.Companhia = op.GetString();

                // ── Origem ───────────────────────────────────────────────────────────────
                if (voo.TryGetProperty("origin", out var origin))
                    resultado.Origem = ExtrairNomeAeroporto(origin);

                // ── Destino ──────────────────────────────────────────────────────────────
                if (voo.TryGetProperty("destination", out var dest))
                    resultado.Destino = ExtrairNomeAeroporto(dest);

                // ── Horários ─────────────────────────────────────────────────────────────
                // actual > estimated > scheduled  (preferir dado real se disponível)
                resultado.HorarioSaida   = ExtrairDataHora(voo, "actual_out", "estimated_out", "scheduled_out");
                resultado.HorarioChegada = ExtrairDataHora(voo, "actual_in",  "estimated_in",  "scheduled_in");

                // ── Duração ──────────────────────────────────────────────────────────────
                if (voo.TryGetProperty("filed_ete", out var ete))
                    resultado.Duracao = ExtrairDuracao(ete);

                // Calcular a partir dos horários se filed_ete não disponível
                if (string.IsNullOrEmpty(resultado.Duracao)
                    && resultado.HorarioSaida.HasValue
                    && resultado.HorarioChegada.HasValue)
                {
                    var dur = resultado.HorarioChegada.Value - resultado.HorarioSaida.Value;
                    if (dur.TotalMinutes > 0)
                        resultado.Duracao = $"{(int)dur.TotalHours}h{dur.Minutes:D2}";
                }

                // ── Status ───────────────────────────────────────────────────────────────
                if (voo.TryGetProperty("status", out var status)
                    && status.ValueKind == JsonValueKind.String)
                    resultado.Status = status.GetString();

                _logger.LogInformation("FlightAware: {Ident} → {Companhia} {Origem} → {Destino} [{Status}]",
                    ident, resultado.Companhia, resultado.Origem, resultado.Destino, resultado.Status);

                return resultado;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede ao consultar FlightAware para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro de conexão com FlightAware. Preencha os campos manualmente." };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao parsear resposta da FlightAware para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro ao processar resposta da FlightAware." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao consultar FlightAware para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro inesperado. Preencha os campos manualmente." };
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static string? ExtrairNomeAeroporto(JsonElement airport)
        {
            var cidade = StrProp(airport, "city");
            var iata   = StrProp(airport, "code_iata");
            var icao   = StrProp(airport, "code");
            var codigo = iata ?? icao;

            return (cidade, codigo) switch
            {
                ({ } cid, { } cod) => $"{cid} ({cod})",
                ({ } cid, null)    => cid,
                (null, { } cod)    => cod,
                _                  => StrProp(airport, "name")
            };
        }

        private static DateTime? ExtrairDataHora(JsonElement voo, params string[] campos)
        {
            foreach (var campo in campos)
            {
                if (!voo.TryGetProperty(campo, out var prop)
                    || prop.ValueKind != JsonValueKind.String) continue;

                var valor = prop.GetString();
                if (string.IsNullOrEmpty(valor)) continue;

                if (DateTime.TryParse(valor, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToLocalTime();
            }
            return null;
        }

        private static string? ExtrairDuracao(JsonElement ete)
        {
            // AeroAPI v4 retorna filed_ete como inteiro (segundos)
            if (ete.ValueKind == JsonValueKind.Number
                && ete.TryGetInt32(out var seg) && seg > 0)
            {
                var dur = TimeSpan.FromSeconds(seg);
                return $"{(int)dur.TotalHours}h{dur.Minutes:D2}";
            }

            if (ete.ValueKind != JsonValueKind.String) return null;

            var s = ete.GetString();
            if (string.IsNullOrEmpty(s)) return null;

            // ISO 8601 duration "PT1H45M" (algumas versões da API)
            if (s.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ts = System.Xml.XmlConvert.ToTimeSpan(s);
                    return $"{(int)ts.TotalHours}h{ts.Minutes:D2}";
                }
                catch { /* fallthrough */ }
            }

            // "HH:MM:SS"
            if (TimeSpan.TryParse(s, out var tsStr) && tsStr.TotalMinutes > 0)
                return $"{(int)tsStr.TotalHours}h{tsStr.Minutes:D2}";

            // Segundos como string "5400"
            if (int.TryParse(s, out var segStr) && segStr > 0)
            {
                var dur = TimeSpan.FromSeconds(segStr);
                return $"{(int)dur.TotalHours}h{dur.Minutes:D2}";
            }

            return null;
        }

        private static string? StrProp(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
    }
}
