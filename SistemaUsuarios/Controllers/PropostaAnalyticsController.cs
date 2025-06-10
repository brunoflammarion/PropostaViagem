using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using System.Text.Json;

namespace SistemaUsuarios.Controllers.Api
{
    [ApiController]
    [Route("api/proposta/analytics")]
    public class PropostaAnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PropostaAnalyticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Método de teste para verificar se a API está funcionando
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "API Analytics funcionando!", timestamp = DateTime.Now });
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartTracking([FromBody] StartTrackingRequest request)
        {
            Console.WriteLine($"🔍 Analytics: Recebido tracking start para proposta {request?.PropostaId}");

            try
            {
                var visualizacao = new PropostaVisualizacao
                {
                    Id = Guid.NewGuid(),
                    PropostaId = request.PropostaId,
                    SessionToken = request.SessionToken,
                    DataHoraInicio = DateTime.Parse(request.DataHoraInicio),
                    TipoDispositivo = request.TipoDispositivo,
                    Navegador = request.Navegador,
                    SistemaOperacional = request.SistemaOperacional,
                    ResolucaoTela = request.ResolucaoTela,
                    IdiomaNavegador = request.IdiomaNavegador,
                    UrlReferenciador = request.UrlReferenciador,
                    TipoReferenciador = request.TipoReferenciador,
                    UserAgent = request.UserAgent,
                    DeviceFingerprint = request.DeviceFingerprint,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    EnderecoIP = GetClientIP(),
                    DataCriacao = DateTime.Now
                };

                // Tentar obter localização via IP (opcional)
                await TryGetLocationFromIP(visualizacao);

                _context.PropostaVisualizacoes.Add(visualizacao);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Analytics: Salvo com sucesso! Session: {request.SessionToken}");

                return Ok(new { success = true, sessionToken = request.SessionToken });
            }
            catch (Exception ex)
            {
                // Log error mas não quebrar o funcionamento
                Console.WriteLine($"❌ Analytics error: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateTracking([FromBody] UpdateTrackingRequest request)
        {
            try
            {
                var visualizacao = await _context.PropostaVisualizacoes
                    .FirstOrDefaultAsync(v => v.SessionToken == request.SessionToken);

                if (visualizacao != null)
                {
                    visualizacao.TempoVisualizacaoSegundos = request.TempoVisualizacaoSegundos;
                    visualizacao.ScrollMaximoPercentual = request.ScrollMaximoPercentual;
                    visualizacao.NumeroCliques = request.NumeroCliques;
                    visualizacao.ClicouWhatsApp = request.ClicouWhatsApp;
                    visualizacao.ClicouEmail = request.ClicouEmail;

                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Analytics update error: {ex.Message}");
                return Ok(new { success = false });
            }
        }

        [HttpPost("interaction")]
        public async Task<IActionResult> TrackInteraction([FromBody] InteractionRequest request)
        {
            try
            {
                var visualizacao = await _context.PropostaVisualizacoes
                    .FirstOrDefaultAsync(v => v.SessionToken == request.SessionToken);

                if (visualizacao != null)
                {
                    // Atualizar flags de interação
                    if (request.InteractionType == "whatsapp_click")
                        visualizacao.ClicouWhatsApp = true;
                    else if (request.InteractionType == "email_click")
                        visualizacao.ClicouEmail = true;

                    // Incrementar cliques
                    visualizacao.NumeroCliques++;

                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Interaction tracking error: {ex.Message}");
                return Ok(new { success = false });
            }
        }

        [HttpPost("finish")]
        public async Task<IActionResult> FinishTracking([FromBody] FinishTrackingRequest request)
        {
            try
            {
                var visualizacao = await _context.PropostaVisualizacoes
                    .FirstOrDefaultAsync(v => v.SessionToken == request.SessionToken);

                if (visualizacao != null)
                {
                    visualizacao.DataHoraFim = DateTime.Parse(request.DataHoraFim);
                    visualizacao.TempoVisualizacaoSegundos = request.TempoVisualizacaoSegundos;
                    visualizacao.ScrollMaximoPercentual = request.ScrollMaximoPercentual;
                    visualizacao.NumeroCliques = request.NumeroCliques;
                    visualizacao.ClicouWhatsApp = request.ClicouWhatsApp;
                    visualizacao.ClicouEmail = request.ClicouEmail;

                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Finish tracking error: {ex.Message}");
                return Ok(new { success = false });
            }
        }

        private string? GetClientIP()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Verificar se está atrás de proxy
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
            else if (Request.Headers.ContainsKey("X-Real-IP"))
                ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();

            return !string.IsNullOrEmpty(ipAddress) ? ipAddress : null;
        }

        private async Task TryGetLocationFromIP(PropostaVisualizacao visualizacao)
        {
            try
            {
                // Só tentar geolocalização se tiver IP válido
                if (string.IsNullOrEmpty(visualizacao.EnderecoIP) || visualizacao.EnderecoIP == "127.0.0.1" || visualizacao.EnderecoIP == "::1")
                    return;

                // Usar serviço gratuito de geolocalização por IP
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                var response = await httpClient.GetStringAsync($"http://ip-api.com/json/{visualizacao.EnderecoIP}");

                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    if (root.TryGetProperty("country", out var country) && !string.IsNullOrEmpty(country.GetString()))
                        visualizacao.Pais = country.GetString();

                    if (root.TryGetProperty("regionName", out var regionName) && !string.IsNullOrEmpty(regionName.GetString()))
                        visualizacao.Estado = regionName.GetString();

                    if (root.TryGetProperty("city", out var city) && !string.IsNullOrEmpty(city.GetString()))
                        visualizacao.Cidade = city.GetString();

                    if (root.TryGetProperty("lat", out var latElement) && latElement.ValueKind == JsonValueKind.Number)
                        visualizacao.Latitude = latElement.GetDecimal();

                    if (root.TryGetProperty("lon", out var lonElement) && lonElement.ValueKind == JsonValueKind.Number)
                        visualizacao.Longitude = lonElement.GetDecimal();
                }
            }
            catch
            {
                // Falha na geolocalização - campos ficam null
                Console.WriteLine("⚠️ Falha na geolocalização por IP");
            }
        }
    }

    // DTOs para as requisições
    public class StartTrackingRequest
    {
        public Guid PropostaId { get; set; }
        public string SessionToken { get; set; }
        public string DataHoraInicio { get; set; }
        public string TipoDispositivo { get; set; }
        public string Navegador { get; set; }
        public string SistemaOperacional { get; set; }
        public string ResolucaoTela { get; set; }
        public string IdiomaNavegador { get; set; }
        public string UrlReferenciador { get; set; }
        public string TipoReferenciador { get; set; }
        public string UserAgent { get; set; }
        public string DeviceFingerprint { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class UpdateTrackingRequest
    {
        public string SessionToken { get; set; }
        public int TempoVisualizacaoSegundos { get; set; }
        public int ScrollMaximoPercentual { get; set; }
        public int NumeroCliques { get; set; }
        public bool ClicouWhatsApp { get; set; }
        public bool ClicouEmail { get; set; }
    }

    public class InteractionRequest
    {
        public string SessionToken { get; set; }
        public string InteractionType { get; set; }
        public string Timestamp { get; set; }
    }

    public class FinishTrackingRequest
    {
        public string SessionToken { get; set; }
        public string DataHoraFim { get; set; }
        public int TempoVisualizacaoSegundos { get; set; }
        public int ScrollMaximoPercentual { get; set; }
        public int NumeroCliques { get; set; }
        public bool ClicouWhatsApp { get; set; }
        public bool ClicouEmail { get; set; }
    }
}