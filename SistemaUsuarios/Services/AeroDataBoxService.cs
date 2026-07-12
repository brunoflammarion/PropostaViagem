using System.Text.Json;

namespace SistemaUsuarios.Services
{
    public interface IFlightLookupService
    {
        Task<FlightInfoResult> ConsultarVooAsync(string codigoVoo);
    }

    public class AeroDataBoxService : IFlightLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AeroDataBoxService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public AeroDataBoxService(HttpClient httpClient, IConfiguration config, ILogger<AeroDataBoxService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey  = config["AeroDataBox:ApiKey"]  ?? string.Empty;
            _baseUrl = (config["AeroDataBox:BaseUrl"] ?? "https://prod.api.market/api/v1/aedbx/aerodatabox").TrimEnd('/');
        }

        public async Task<FlightInfoResult> ConsultarVooAsync(string codigoVoo)
        {
            if (string.IsNullOrWhiteSpace(codigoVoo))
                return new FlightInfoResult { Erro = "Código do voo não informado." };

            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "CONFIGURE_VIA_USER_SECRETS")
                return new FlightInfoResult { Erro = "Chave AeroDataBox não configurada." };

            var ident = codigoVoo.Trim().ToUpperInvariant().Replace(" ", "");
            var url   = $"{_baseUrl}/flights/Number/{Uri.EscapeDataString(ident)}?withAircraftImage=false&withLocation=false&withFlightPlan=false";

            _logger.LogDebug("AeroDataBox: GET {Url}", url);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("x-magicapi-key", _apiKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("AeroDataBox {Status} para {Ident}: {Body}", (int)response.StatusCode, ident, body);

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound =>
                            new FlightInfoResult { Erro = $"Voo {ident} não encontrado. Verifique o código." },
                        System.Net.HttpStatusCode.Unauthorized =>
                            new FlightInfoResult { Erro = "Chave de API inválida. Verifique AeroDataBox:ApiKey." },
                        System.Net.HttpStatusCode.TooManyRequests =>
                            new FlightInfoResult { Erro = "Limite de consultas atingido. Tente novamente em instantes." },
                        _ =>
                            new FlightInfoResult { Erro = $"Erro ao consultar AeroDataBox ({(int)response.StatusCode})." }
                    };
                }

                var json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("AeroDataBox retornou corpo vazio para {Ident}", ident);
                    return new FlightInfoResult { Erro = $"Voo {ident} não encontrado ou sem dados disponíveis." };
                }

                _logger.LogDebug("AeroDataBox: {Bytes} bytes para {Ident}", json.Length, ident);
                return ParseResposta(json, ident);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede AeroDataBox para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro de conexão com AeroDataBox. Preencha os campos manualmente." };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao parsear resposta AeroDataBox para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro ao processar resposta da API. Preencha os campos manualmente." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado AeroDataBox para {Ident}", ident);
                return new FlightInfoResult { Erro = "Erro inesperado. Preencha os campos manualmente." };
            }
        }

        // ── Parser ───────────────────────────────────────────────────────────────

        private static FlightInfoResult ParseResposta(string json, string ident)
        {
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement voo = default;
            bool found = false;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            { voo = root[0]; found = true; }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("departures", out var deps) && deps.GetArrayLength() > 0)
                { voo = deps[0]; found = true; }
                else if (root.TryGetProperty("arrivals", out var arrs) && arrs.GetArrayLength() > 0)
                { voo = arrs[0]; found = true; }
            }

            if (!found)
                return new FlightInfoResult { Erro = $"Nenhum voo encontrado para {ident}." };

            var r = new FlightInfoResult { CodigoBusca = ident };

            // ── Número do voo ────────────────────────────────────────────────────
            r.CodigoVoo = StrProp(voo, "number") ?? ident;
            r.IdentIata = r.CodigoVoo;

            // ── Companhia ────────────────────────────────────────────────────────
            if (voo.TryGetProperty("airline", out var airline))
            {
                var nome = StrProp(airline, "name");
                var iata = StrProp(airline, "iata");
                var icao = StrProp(airline, "icao");
                r.CompanhiaIata = iata;
                r.CompanhiaIcao = icao;
                r.Companhia     = !string.IsNullOrEmpty(nome) ? nome
                                : !string.IsNullOrEmpty(iata) ? iata
                                : icao;
            }

            // ── Status / Aeronave ────────────────────────────────────────────────
            r.Status    = StrProp(voo, "status");
            r.Codeshare = StrProp(voo, "codeshareStatus");

            if (voo.TryGetProperty("aircraft", out var aircraft))
                r.ModeloAeronave = StrProp(aircraft, "model") ?? StrProp(aircraft, "reg");

            if (voo.TryGetProperty("lastUpdatedUtc", out var lastUpd) && lastUpd.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(lastUpd.GetString(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lu))
                    r.UltimaAtualizacao = lu.ToLocalTime();

            // ── Origem ───────────────────────────────────────────────────────────
            if (voo.TryGetProperty("departure", out var dep))
            {
                if (dep.TryGetProperty("airport", out var ap))
                {
                    r.OrigemIata      = StrProp(ap, "iata");
                    r.OrigemIcao      = StrProp(ap, "icao");
                    r.OrigemNomeCurto = StrProp(ap, "shortName");
                    r.OrigemCidade    = StrProp(ap, "municipalityName");
                    r.OrigemPais      = StrProp(ap, "countryCode");
                    r.OrigemFuso      = StrProp(ap, "timeZone");
                    r.Origem          = FormatarAeroporto(ap);
                }

                ExtrairHorariosSegmento(dep, "scheduledTime", "revisedTime", null,
                    out var saidaLocProg, out var saidaUtcProg,
                    out var saidaLocRev,  out var saidaUtcRev,
                    out _,               out _);

                r.SaidaLocalProgramada = saidaLocProg;
                r.SaidaUtcProgramada   = saidaUtcProg;
                r.SaidaLocalRevisada   = saidaLocRev;
                r.SaidaUtcRevisada     = saidaUtcRev;

                // Prioridade: revisedTime.local → scheduledTime.local
                r.HorarioSaida = saidaLocRev ?? saidaLocProg;

                r.OrigemTerminal = StrProp(dep, "terminal");
                r.OrigemPortao   = StrProp(dep, "gate");
                r.OrigemCheckIn  = StrProp(dep, "checkInDesk");
            }

            // ── Destino ──────────────────────────────────────────────────────────
            if (voo.TryGetProperty("arrival", out var arr))
            {
                if (arr.TryGetProperty("airport", out var ap))
                {
                    r.DestinoIata      = StrProp(ap, "iata");
                    r.DestinoIcao      = StrProp(ap, "icao");
                    r.DestinoNomeCurto = StrProp(ap, "shortName");
                    r.DestinoCidade    = StrProp(ap, "municipalityName");
                    r.DestinoPais      = StrProp(ap, "countryCode");
                    r.DestinoFuso      = StrProp(ap, "timeZone");
                    r.Destino          = FormatarAeroporto(ap);
                }

                ExtrairHorariosSegmento(arr, "scheduledTime", "revisedTime", "predictedTime",
                    out var chegLocProg, out var chegUtcProg,
                    out var chegLocRev,  out var chegUtcRev,
                    out var chegLocPrev, out var chegUtcPrev);

                r.ChegadaLocalProgramada = chegLocProg;
                r.ChegadaUtcProgramada   = chegUtcProg;
                r.ChegadaLocalRevisada   = chegLocRev;
                r.ChegadaUtcRevisada     = chegUtcRev;
                r.ChegadaLocalPrevista   = chegLocPrev;
                r.ChegadaUtcPrevista     = chegUtcPrev;

                // Prioridade: predictedTime.local → revisedTime.local → scheduledTime.local
                r.HorarioChegada = chegLocPrev ?? chegLocRev ?? chegLocProg;
            }

            // ── Duração ──────────────────────────────────────────────────────────
            if (r.HorarioSaida.HasValue && r.HorarioChegada.HasValue)
            {
                var diff = r.HorarioChegada.Value - r.HorarioSaida.Value;
                if (diff.TotalMinutes > 0)
                    r.Duracao = $"{(int)diff.TotalHours}h{diff.Minutes:D2}";
            }

            return r;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string? FormatarAeroporto(JsonElement ap)
        {
            var city = StrProp(ap, "municipalityName") ?? StrProp(ap, "shortName");
            var iata = StrProp(ap, "iata");
            if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(iata)) return $"{city} ({iata})";
            if (!string.IsNullOrEmpty(city)) return city;
            if (!string.IsNullOrEmpty(iata)) return iata;
            return StrProp(ap, "name");
        }

        // Extrai par local/UTC dos campos scheduledTime, revisedTime, predictedTime
        private static void ExtrairHorariosSegmento(
            JsonElement segmento,
            string keyScheduled, string keyRevised, string? keyPredicted,
            out DateTime? locProg,  out DateTime? utcProg,
            out DateTime? locRev,   out DateTime? utcRev,
            out DateTime? locPrev,  out DateTime? utcPrev)
        {
            locProg = utcProg = locRev = utcRev = locPrev = utcPrev = null;

            if (segmento.TryGetProperty(keyScheduled, out var sched))
            {
                locProg = ParseLocalTime(StrProp(sched, "local"));
                utcProg = ParseUtcTime(StrProp(sched, "utc"));
            }

            if (segmento.TryGetProperty(keyRevised, out var rev))
            {
                locRev = ParseLocalTime(StrProp(rev, "local"));
                utcRev = ParseUtcTime(StrProp(rev, "utc"));
            }

            if (keyPredicted != null && segmento.TryGetProperty(keyPredicted, out var pred))
            {
                locPrev = ParseLocalTime(StrProp(pred, "local"));
                utcPrev = ParseUtcTime(StrProp(pred, "utc"));
            }
        }

        // Brasil é permanentemente UTC-3 desde 2019 (sem horário de verão)
        private static readonly TimeSpan BrtOffset = TimeSpan.FromHours(-3);

        private static DateTime? ParseLocalTime(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return null;
            if (DateTime.TryParse(valor, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                // Se a API retornou UTC no campo "local", converter para BRT
                if (dt.Kind == DateTimeKind.Utc)
                    return DateTime.SpecifyKind(dt + BrtOffset, DateTimeKind.Unspecified);
                // Sem offset → assumir que já é horário local do aeroporto
                return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            }
            return null;
        }

        private static DateTime? ParseUtcTime(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return null;
            if (DateTime.TryParse(valor, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToUniversalTime();
            return null;
        }

        private static string? StrProp(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;
    }
}
