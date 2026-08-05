using System.Text.Json;
using System.Text.Json.Serialization;

namespace SistemaUsuarios.Services
{
    public interface IFlightLookupService
    {
        // candidatoIndice: -1 = auto-selecionar; >= 0 = escolha explícita do usuário
        Task<FlightInfoResult> ConsultarVooAsync(string codigoVoo, DateOnly dataVoo, int candidatoIndice = -1);
    }

    public class AeroDataBoxService : IFlightLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AeroDataBoxService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AeroDataBoxService(HttpClient httpClient, IConfiguration config, ILogger<AeroDataBoxService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey  = config["AeroDataBox:ApiKey"]  ?? string.Empty;
            _baseUrl = (config["AeroDataBox:BaseUrl"] ?? "https://prod.api.market/api/v1/aedbx/aerodatabox").TrimEnd('/');
        }

        public async Task<FlightInfoResult> ConsultarVooAsync(string codigoVoo, DateOnly dataVoo, int candidatoIndice = -1)
        {
            if (string.IsNullOrWhiteSpace(codigoVoo))
                return Erro("Código do voo não informado.");

            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "CONFIGURE_VIA_USER_SECRETS")
                return Erro("Chave AeroDataBox não configurada.");

            var ident   = codigoVoo.Trim().ToUpperInvariant().Replace(" ", "");
            var dataStr = dataVoo.ToString("yyyy-MM-dd");
            var url = $"{_baseUrl}/flights/Number/{Uri.EscapeDataString(ident)}/{dataStr}" +
                      "?dateLocalRole=Both&withAircraftImage=true&withLocation=true&withFlightPlan=false";

            _logger.LogInformation("AeroDataBox ConsultarVoo | ident={Ident} data={Data}", ident, dataStr);

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
                            Erro($"Voo {ident} não encontrado. Verifique o código."),
                        System.Net.HttpStatusCode.Unauthorized =>
                            Erro("Chave de API inválida. Verifique AeroDataBox:ApiKey."),
                        System.Net.HttpStatusCode.TooManyRequests =>
                            Erro("Limite de consultas atingido. Tente novamente em instantes."),
                        _ =>
                            Erro($"Erro ao consultar AeroDataBox ({(int)response.StatusCode}).")
                    };
                }

                var json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("AeroDataBox retornou corpo vazio para {Ident}", ident);
                    return Erro($"Voo {ident} não encontrado ou sem dados disponíveis.");
                }

                _logger.LogDebug("AeroDataBox: {Bytes} bytes para {Ident}", json.Length, ident);
                return ParseResposta(json, ident, dataVoo, candidatoIndice, _logger);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede AeroDataBox para {Ident}", ident);
                return Erro("Erro de conexão com AeroDataBox. Preencha os campos manualmente.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao parsear resposta AeroDataBox para {Ident}", ident);
                return Erro("Erro ao processar resposta da API. Preencha os campos manualmente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado AeroDataBox para {Ident}", ident);
                return Erro("Erro inesperado. Preencha os campos manualmente.");
            }
        }

        // ── Parser principal ─────────────────────────────────────────────────────

        private static FlightInfoResult ParseResposta(
            string json, string ident, DateOnly dataVoo, int candidatoIndice, ILogger logger)
        {
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Coletar todos os candidatos preservando índice original
            var candidatos = new List<(int Indice, AdbFlightDto Dto)>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var el in root.EnumerateArray())
                {
                    var dto = JsonSerializer.Deserialize<AdbFlightDto>(el.GetRawText(), _jsonOptions);
                    if (dto != null) candidatos.Add((i++, dto));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                int i = 0;
                if (root.TryGetProperty("departures", out var deps))
                    foreach (var el in deps.EnumerateArray())
                    {
                        var dto = JsonSerializer.Deserialize<AdbFlightDto>(el.GetRawText(), _jsonOptions);
                        if (dto != null) candidatos.Add((i++, dto));
                    }
                if (root.TryGetProperty("arrivals", out var arrs))
                    foreach (var el in arrs.EnumerateArray())
                    {
                        var dto = JsonSerializer.Deserialize<AdbFlightDto>(el.GetRawText(), _jsonOptions);
                        if (dto != null) candidatos.Add((i++, dto));
                    }
            }

            if (candidatos.Count == 0)
                return Erro($"Nenhum voo encontrado para {ident}.");

            // Se candidatoIndice especificado, busca direto pelo índice original
            if (candidatoIndice >= 0)
            {
                var escolhido = candidatos.FirstOrDefault(c => c.Indice == candidatoIndice);
                if (escolhido.Dto == null)
                    return Erro("Candidato selecionado não encontrado. Tente a busca novamente.");
                return MapearVoo(escolhido.Dto, ident);
            }

            // Filtrar pelo dia local de partida
            var correspondentes = candidatos
                .Where(c => DataLocalPartida(c.Dto) is DateTimeOffset dto &&
                            DateOnly.FromDateTime(dto.DateTime) == dataVoo)
                .ToList();

            logger.LogInformation(
                "AeroDataBox | ident={Ident} data={Data} total={Total} correspondentes={Corr}",
                ident, dataVoo, candidatos.Count, correspondentes.Count);

            if (correspondentes.Count == 0)
                return Erro(
                    $"Encontramos informações para o voo {ident}, mas não para {dataVoo:dd/MM/yyyy}. " +
                    "Verifique a data, a companhia e o número informados.");

            if (correspondentes.Count == 1)
                return MapearVoo(correspondentes[0].Dto, ident);

            // Múltiplos correspondentes — retorna lista para desambiguação pelo usuário
            logger.LogWarning(
                "AeroDataBox múltiplos correspondentes | ident={Ident} data={Data} count={N}",
                ident, dataVoo, correspondentes.Count);

            var result = new FlightInfoResult { CodigoBusca = ident };
            result.MultiplosCandidatos = correspondentes.Select(c => new VooCandidatoSimples
            {
                Indice         = c.Indice,
                CodigoVoo      = c.Dto.Number,
                Companhia      = c.Dto.Airline?.Name ?? c.Dto.Airline?.Iata ?? c.Dto.Airline?.Icao,
                Origem         = FormatarAeroporto(c.Dto.Departure?.Airport),
                OrigemIata     = c.Dto.Departure?.Airport?.Iata,
                Destino        = FormatarAeroporto(c.Dto.Arrival?.Airport),
                DestinoIata    = c.Dto.Arrival?.Airport?.Iata,
                HorarioSaida   = ParseLocalOffset(c.Dto.Departure?.ScheduledTime?.Local)?.ToString("yyyy-MM-ddTHH:mm"),
                HorarioChegada = ParseLocalOffset(c.Dto.Arrival?.ScheduledTime?.Local)?.ToString("yyyy-MM-ddTHH:mm"),
                Status         = c.Dto.Status,
            }).ToList();
            return result;
        }

        // ── Mapeamento de um candidato selecionado ───────────────────────────────

        private static FlightInfoResult MapearVoo(AdbFlightDto voo, string ident)
        {
            var r = new FlightInfoResult { CodigoBusca = ident };

            r.CodigoVoo = voo.Number ?? ident;
            r.IdentIata = r.CodigoVoo;
            r.IsCargo   = voo.IsCargo;
            r.Status    = voo.Status;
            r.Codeshare = voo.CodeshareStatus;

            if (!string.IsNullOrEmpty(voo.LastUpdatedUtc) &&
                DateTimeOffset.TryParse(voo.LastUpdatedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lu))
                r.UltimaAtualizacao = lu.UtcDateTime;

            // Companhia
            if (voo.Airline != null)
            {
                r.CompanhiaIata = voo.Airline.Iata;
                r.CompanhiaIcao = voo.Airline.Icao;
                r.Companhia     = voo.Airline.Name ?? voo.Airline.Iata ?? voo.Airline.Icao;
            }

            // Aeronave
            if (voo.Aircraft != null)
            {
                r.ModeloAeronave = voo.Aircraft.Model ?? voo.Aircraft.Reg;
                if (voo.Aircraft.Image != null)
                {
                    r.ImagemAeronaveUrl             = voo.Aircraft.Image.Url ?? voo.Aircraft.Image.WebUrl;
                    r.ImagemAeronaveAutor           = voo.Aircraft.Image.Author;
                    r.ImagemAeronaveTitulo          = voo.Aircraft.Image.Title;
                    r.ImagemAeronautaHtmlAtribuicoes = voo.Aircraft.Image.HtmlAttributions;
                }
            }

            // Distância
            if (voo.GreatCircleDistance != null)
            {
                r.DistanciaKm             = voo.GreatCircleDistance.Km;
                r.DistanciaMilhas         = voo.GreatCircleDistance.Mile;
                r.DistanciaMetros         = voo.GreatCircleDistance.Meter;
                r.DistanciaMilhasNauticas = voo.GreatCircleDistance.Nm;
                r.DistanciaPes            = voo.GreatCircleDistance.Feet;
            }

            // Origem
            if (voo.Departure != null)
            {
                var dep = voo.Departure;
                if (dep.Airport != null)
                {
                    var ap = dep.Airport;
                    r.OrigemIata      = ap.Iata;
                    r.OrigemIcao      = ap.Icao;
                    r.OrigemNome      = ap.Name;
                    r.OrigemNomeCurto = ap.ShortName;
                    r.OrigemCidade    = ap.MunicipalityName;
                    r.OrigemPais      = ap.CountryCode;
                    r.OrigemFuso      = ap.TimeZone;
                    r.OrigemLatitude  = ap.Location?.Lat;
                    r.OrigemLongitude = ap.Location?.Lon;
                    r.Origem          = FormatarAeroporto(ap);
                }
                r.OrigemTerminal = dep.Terminal;
                r.OrigemPortao   = dep.Gate;
                r.OrigemCheckIn  = dep.CheckInDesk;

                var sProg = ParseLocalOffset(dep.ScheduledTime?.Local);
                r.SaidaLocalProgramada = sProg?.DateTime;
                r.SaidaUtcProgramada   = ParseUtcString(dep.ScheduledTime?.Utc);

                var sPrev = ParseLocalOffset(dep.PredictedTime?.Local ?? dep.EstimatedTime?.Local);
                r.SaidaLocalPrevista = sPrev?.DateTime;
                r.SaidaUtcPrevista   = ParseUtcString(dep.PredictedTime?.Utc ?? dep.EstimatedTime?.Utc);

                var sRev = ParseLocalOffset(dep.RevisedTime?.Local);
                r.SaidaLocalRevisada = sRev?.DateTime;
                r.SaidaUtcRevisada   = ParseUtcString(dep.RevisedTime?.Utc);
            }

            // Destino
            if (voo.Arrival != null)
            {
                var arr = voo.Arrival;
                if (arr.Airport != null)
                {
                    var ap = arr.Airport;
                    r.DestinoIata      = ap.Iata;
                    r.DestinoIcao      = ap.Icao;
                    r.DestinoNome      = ap.Name;
                    r.DestinoNomeCurto = ap.ShortName;
                    r.DestinoCidade    = ap.MunicipalityName;
                    r.DestinoPais      = ap.CountryCode;
                    r.DestinoFuso      = ap.TimeZone;
                    r.DestinoLatitude  = ap.Location?.Lat;
                    r.DestinoLongitude = ap.Location?.Lon;
                    r.Destino          = FormatarAeroporto(ap);
                }

                var cProg = ParseLocalOffset(arr.ScheduledTime?.Local);
                r.ChegadaLocalProgramada = cProg?.DateTime;
                r.ChegadaUtcProgramada   = ParseUtcString(arr.ScheduledTime?.Utc);

                var cPrev = ParseLocalOffset(arr.PredictedTime?.Local ?? arr.EstimatedTime?.Local);
                r.ChegadaLocalPrevista = cPrev?.DateTime;
                r.ChegadaUtcPrevista   = ParseUtcString(arr.PredictedTime?.Utc ?? arr.EstimatedTime?.Utc);

                var cRev = ParseLocalOffset(arr.RevisedTime?.Local);
                r.ChegadaLocalRevisada = cRev?.DateTime;
                r.ChegadaUtcRevisada   = ParseUtcString(arr.RevisedTime?.Utc);
            }

            // Horário principal: exclusivamente scheduledTime.local — sem substituição por
            // revisedTime/predictedTime/estimatedTime, mesmo que presentes na resposta.
            var saidaOff   = ParseLocalOffset(voo.Departure?.ScheduledTime?.Local);
            var chegadaOff = ParseLocalOffset(voo.Arrival?.ScheduledTime?.Local);

            r.HorarioSaida   = saidaOff?.DateTime;
            r.HorarioChegada = chegadaOff?.DateTime;

            if (saidaOff.HasValue && chegadaOff.HasValue)
            {
                var diff = chegadaOff.Value - saidaOff.Value;
                if (diff.TotalMinutes > 0)
                {
                    var totalMin = (int)diff.TotalMinutes;
                    var horas    = totalMin / 60;
                    var minutos  = totalMin % 60;
                    r.Duracao = horas >= 24
                        ? $"{horas / 24}d {horas % 24}h{minutos:D2}"
                        : $"{horas}h{minutos:D2}";
                }

                // Chegada em dia posterior (comparando datas locais de cada aeroporto)
                var diasDif = (chegadaOff.Value.DateTime.Date - saidaOff.Value.DateTime.Date).Days;
                if (diasDif > 0) r.DiasSeguintesChegada = diasDif;
            }

            return r;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static DateTimeOffset? DataLocalPartida(AdbFlightDto voo)
        {
            var dep = voo.Departure;
            if (dep == null) return null;
            return ParseLocalOffset(dep.RevisedTime?.Local)
                ?? ParseLocalOffset(dep.ScheduledTime?.Local);
        }

        // Interpreta horário local com offset (ex: "2026-08-05 11:00-03:00")
        // Preserva o offset do aeroporto sem conversão adicional
        private static DateTimeOffset? ParseLocalOffset(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return null;
            if (DateTimeOffset.TryParse(valor,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dto))
                return dto;
            if (DateTime.TryParse(valor,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return new DateTimeOffset(dt, TimeSpan.Zero);
            return null;
        }

        private static DateTime? ParseUtcString(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return null;
            if (DateTimeOffset.TryParse(valor, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
                return dto.UtcDateTime;
            return null;
        }

        private static string? FormatarAeroporto(AdbAirportDto? ap)
        {
            if (ap == null) return null;
            var cidade = ap.MunicipalityName ?? ap.ShortName;
            var iata   = ap.Iata;
            if (!string.IsNullOrEmpty(cidade) && !string.IsNullOrEmpty(iata)) return $"{cidade} ({iata})";
            if (!string.IsNullOrEmpty(cidade)) return cidade;
            if (!string.IsNullOrEmpty(iata))   return iata;
            return ap.Name;
        }

        private static FlightInfoResult Erro(string msg) => new() { Erro = msg };
    }

    // ── DTOs internos — mapeiam o JSON bruto da AeroDataBox ─────────────────────

    internal class AdbFlightDto
    {
        [JsonPropertyName("number")]              public string?         Number              { get; set; }
        [JsonPropertyName("status")]              public string?         Status              { get; set; }
        [JsonPropertyName("codeshareStatus")]     public string?         CodeshareStatus     { get; set; }
        [JsonPropertyName("isCargo")]             public bool            IsCargo             { get; set; }
        [JsonPropertyName("lastUpdatedUtc")]      public string?         LastUpdatedUtc      { get; set; }
        [JsonPropertyName("airline")]             public AdbAirlineDto?  Airline             { get; set; }
        [JsonPropertyName("aircraft")]            public AdbAircraftDto? Aircraft            { get; set; }
        [JsonPropertyName("departure")]           public AdbMovementDto? Departure           { get; set; }
        [JsonPropertyName("arrival")]             public AdbMovementDto? Arrival             { get; set; }
        [JsonPropertyName("greatCircleDistance")] public AdbDistanceDto? GreatCircleDistance { get; set; }
    }

    internal class AdbAirlineDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("iata")] public string? Iata { get; set; }
        [JsonPropertyName("icao")] public string? Icao { get; set; }
    }

    internal class AdbAircraftDto
    {
        [JsonPropertyName("model")] public string?              Model { get; set; }
        [JsonPropertyName("reg")]   public string?              Reg   { get; set; }
        [JsonPropertyName("image")] public AdbAircraftImageDto? Image { get; set; }
    }

    internal class AdbAircraftImageDto
    {
        [JsonPropertyName("url")]              public string?       Url              { get; set; }
        [JsonPropertyName("webUrl")]           public string?       WebUrl           { get; set; }
        [JsonPropertyName("author")]           public string?       Author           { get; set; }
        [JsonPropertyName("title")]            public string?       Title            { get; set; }
        [JsonPropertyName("description")]      public string?       Description      { get; set; }
        [JsonPropertyName("license")]          public string?       License          { get; set; }
        [JsonPropertyName("htmlAttributions")] public List<string>? HtmlAttributions { get; set; }
    }

    internal class AdbMovementDto
    {
        [JsonPropertyName("airport")]       public AdbAirportDto? Airport       { get; set; }
        [JsonPropertyName("scheduledTime")] public AdbTimeDto?    ScheduledTime { get; set; }
        [JsonPropertyName("predictedTime")] public AdbTimeDto?    PredictedTime { get; set; }
        [JsonPropertyName("estimatedTime")] public AdbTimeDto?    EstimatedTime { get; set; }
        [JsonPropertyName("revisedTime")]   public AdbTimeDto?    RevisedTime   { get; set; }
        [JsonPropertyName("terminal")]      public string?        Terminal      { get; set; }
        [JsonPropertyName("gate")]          public string?        Gate          { get; set; }
        [JsonPropertyName("checkInDesk")]   public string?        CheckInDesk   { get; set; }
        [JsonPropertyName("quality")]       public List<string>?  Quality       { get; set; }
    }

    internal class AdbTimeDto
    {
        [JsonPropertyName("utc")]   public string? Utc   { get; set; }
        [JsonPropertyName("local")] public string? Local { get; set; }
    }

    internal class AdbAirportDto
    {
        [JsonPropertyName("icao")]             public string?         Icao             { get; set; }
        [JsonPropertyName("iata")]             public string?         Iata             { get; set; }
        [JsonPropertyName("name")]             public string?         Name             { get; set; }
        [JsonPropertyName("shortName")]        public string?         ShortName        { get; set; }
        [JsonPropertyName("municipalityName")] public string?         MunicipalityName { get; set; }
        [JsonPropertyName("countryCode")]      public string?         CountryCode      { get; set; }
        [JsonPropertyName("timeZone")]         public string?         TimeZone         { get; set; }
        [JsonPropertyName("location")]         public AdbLocationDto? Location         { get; set; }
    }

    internal class AdbLocationDto
    {
        [JsonPropertyName("lat")] public double? Lat { get; set; }
        [JsonPropertyName("lon")] public double? Lon { get; set; }
    }

    internal class AdbDistanceDto
    {
        [JsonPropertyName("meter")] public decimal? Meter { get; set; }
        [JsonPropertyName("km")]    public decimal? Km    { get; set; }
        [JsonPropertyName("mile")]  public decimal? Mile  { get; set; }
        [JsonPropertyName("nm")]    public decimal? Nm    { get; set; }
        [JsonPropertyName("feet")]  public decimal? Feet  { get; set; }
    }
}
