using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Services
{
    public class AiGatewayRequest
    {
        public Guid AgenciaId { get; set; }
        public Guid UsuarioId { get; set; }
        public string Funcionalidade { get; set; } = "";
        public string Modelo { get; set; } = "gpt-4o-mini";
        public object Payload { get; set; } = new();
        public Guid? PropostaId { get; set; }
        public string? EntidadeId { get; set; }
        public string? EntidadeTipo { get; set; }
    }

    public class AiGatewayResult
    {
        public bool Sucesso { get; set; }
        public string? Conteudo { get; set; }
        public string? CodigoErro { get; set; }
        public string? MensagemErro { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal CustoTotal { get; set; }
        public string CorrelationId { get; set; } = "";
    }

    public interface IAiGatewayService
    {
        Task<AiGatewayResult> ExecutarAsync(AiGatewayRequest request, CancellationToken ct = default);
    }

    public class AiGatewayService : IAiGatewayService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiGatewayService> _logger;

        public AiGatewayService(
            ApplicationDbContext db,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AiGatewayService> logger)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AiGatewayResult> ExecutarAsync(AiGatewayRequest request, CancellationToken ct = default)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var inicio = DateTime.UtcNow;

            // 1. Verificar limite da agência
            var limite = await _db.AiAgencyLimits
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.AgenciaId == request.AgenciaId && l.Ativo, ct);

            if (limite != null && limite.ModoControle == AiModoControle.Bloqueio && limite.LimiteMensalCusto.HasValue)
            {
                var periodoInicio = new DateTime(inicio.Year, inicio.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var periodoFim = periodoInicio.AddMonths(1);

                var consumoMes = await _db.AiUsageRecords
                    .Where(r => r.AgenciaId == request.AgenciaId
                             && r.DataHoraInicio >= periodoInicio
                             && r.DataHoraInicio < periodoFim
                             && r.Sucesso)
                    .SumAsync(r => (decimal?)r.CustoTotal, ct) ?? 0m;

                var teto = limite.PermitirExcedente && limite.ValorExcedentePermitido.HasValue
                    ? limite.LimiteMensalCusto.Value + limite.ValorExcedentePermitido.Value
                    : limite.LimiteMensalCusto.Value;

                if (consumoMes >= teto)
                {
                    await RegistrarBloqueio(request, correlationId, inicio, consumoMes, ct);
                    return new AiGatewayResult
                    {
                        Sucesso = false,
                        CodigoErro = "AI_MONTHLY_LIMIT_REACHED",
                        MensagemErro = "Limite mensal de IA atingido.",
                        CorrelationId = correlationId
                    };
                }
            }

            // 2. Chamar OpenAI
            var pricing = await ObterPrecificacao(request.Modelo, ct);
            var sw = Stopwatch.StartNew();
            string? conteudo = null;
            int inputTokens = 0, cachedTokens = 0, outputTokens = 0;
            bool sucesso = false;
            int? httpStatus = null;
            string? tipoErro = null;
            string? erroSanitizado = null;
            string? providerRequestId = null;

            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"]
                    ?? throw new InvalidOperationException("OpenAI:ApiKey não configurado.");

                var client = _httpClientFactory.CreateClient("OpenAI");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var json = JsonSerializer.Serialize(request.Payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, ct);

                httpStatus = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    providerRequestId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        conteudo = choices[0].GetProperty("message").GetProperty("content").GetString();

                    if (root.TryGetProperty("usage", out var usage))
                    {
                        inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                        outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
                        if (usage.TryGetProperty("prompt_tokens_details", out var ptd)
                            && ptd.TryGetProperty("cached_tokens", out var cached))
                            cachedTokens = cached.GetInt32();
                    }

                    sucesso = true;
                }
                else
                {
                    tipoErro = "ApiError";
                    erroSanitizado = $"HTTP {httpStatus}";
                }
            }
            catch (TaskCanceledException)
            {
                tipoErro = "Timeout";
                erroSanitizado = "Timeout na chamada à API";
            }
            catch (OperationCanceledException)
            {
                tipoErro = "Cancelled";
                erroSanitizado = "Requisição cancelada";
            }
            catch (Exception ex)
            {
                tipoErro = ex.GetType().Name;
                erroSanitizado = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
                _logger.LogError(ex, "Erro inesperado no AiGatewayService [corr={CorrelationId}]", correlationId);
            }
            finally
            {
                sw.Stop();
            }

            // 3. Calcular custo
            var (precoInput, precoCached, precoOutput) = pricing;
            var billedInput = inputTokens - cachedTokens;
            var custoInput = billedInput * precoInput / 1_000_000m;
            var custoCached = cachedTokens * precoCached / 1_000_000m;
            var custoOutput = outputTokens * precoOutput / 1_000_000m;
            var custoTotal = custoInput + custoCached + custoOutput;

            // 4. Persistir registro
            var record = new AiUsageRecord
            {
                AgenciaId = request.AgenciaId,
                UsuarioId = request.UsuarioId,
                PropostaId = request.PropostaId,
                EntidadeId = request.EntidadeId,
                EntidadeTipo = request.EntidadeTipo,
                Funcionalidade = request.Funcionalidade,
                Provedor = "OpenAI",
                Modelo = request.Modelo,
                CorrelationId = correlationId,
                ProviderRequestId = providerRequestId,
                InputTokens = inputTokens,
                CachedInputTokens = cachedTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                PrecoInputPorMilhao = precoInput,
                PrecoCachedInputPorMilhao = precoCached,
                PrecoOutputPorMilhao = precoOutput,
                CustoInput = custoInput,
                CustoCachedInput = custoCached,
                CustoOutput = custoOutput,
                CustoTotal = custoTotal,
                Moeda = "USD",
                Sucesso = sucesso,
                Status = sucesso ? "Success" : (tipoErro ?? "Error"),
                DuracaoMs = (int)sw.ElapsedMilliseconds,
                HttpStatusExterno = httpStatus,
                TipoErro = tipoErro,
                MensagemErroSanitizada = erroSanitizado,
                DataHoraInicio = inicio,
                DataHoraFim = DateTime.UtcNow,
                CriadoEm = DateTime.UtcNow
            };

            try
            {
                _db.AiUsageRecords.Add(record);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Não falhar a chamada se o registro não persistir
                _logger.LogError(ex, "Falha ao persistir AiUsageRecord [corr={CorrelationId}]", correlationId);
            }

            return new AiGatewayResult
            {
                Sucesso = sucesso,
                Conteudo = conteudo,
                CodigoErro = sucesso ? null : (tipoErro ?? "Error"),
                MensagemErro = erroSanitizado,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                CustoTotal = custoTotal,
                CorrelationId = correlationId
            };
        }

        private async Task RegistrarBloqueio(AiGatewayRequest request, string correlationId,
            DateTime inicio, decimal consumoAtual, CancellationToken ct)
        {
            try
            {
                var record = new AiUsageRecord
                {
                    AgenciaId = request.AgenciaId,
                    UsuarioId = request.UsuarioId,
                    PropostaId = request.PropostaId,
                    EntidadeId = request.EntidadeId,
                    EntidadeTipo = request.EntidadeTipo,
                    Funcionalidade = request.Funcionalidade,
                    Provedor = "OpenAI",
                    Modelo = request.Modelo,
                    CorrelationId = correlationId,
                    Sucesso = false,
                    Status = "Blocked",
                    TipoErro = "LimitReached",
                    MensagemErroSanitizada = $"Limite mensal atingido (consumo atual: {consumoAtual:F4} USD)",
                    DataHoraInicio = inicio,
                    DataHoraFim = inicio,
                    CriadoEm = DateTime.UtcNow
                };
                _db.AiUsageRecords.Add(record);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao registrar bloqueio [corr={CorrelationId}]", correlationId);
            }
        }

        private async Task<(decimal input, decimal cached, decimal output)> ObterPrecificacao(
            string modelo, CancellationToken ct)
        {
            var pricing = await _db.AiModelPricings
                .AsNoTracking()
                .Where(p => p.Modelo == modelo && p.Ativo)
                .OrderByDescending(p => p.VigenciaInicio)
                .FirstOrDefaultAsync(ct);

            if (pricing != null)
                return (pricing.PrecoInputPorMilhao, pricing.PrecoCachedInputPorMilhao, pricing.PrecoOutputPorMilhao);

            // Fallback: gpt-4o-mini defaults
            return modelo.Contains("gpt-4o-mini") ? (0.15m, 0.075m, 0.60m) : (2.50m, 1.25m, 10.00m);
        }
    }
}
