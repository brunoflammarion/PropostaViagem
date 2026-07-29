namespace SistemaUsuarios.Infrastructure;

/// <summary>
/// Conversão de instantes UTC para horário de Brasília (America/Sao_Paulo).
/// Use em Views e qualquer camada de apresentação para exibir datas ao usuário.
/// NÃO use em campos civis (DataNascimento, datas de viagem, CheckIn/CheckOut).
/// </summary>
public static class DateTimeExtensions
{
    // Windows: "E. South America Standard Time" | Linux/macOS: "America/Sao_Paulo"
    private static readonly TimeZoneInfo BrazilTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

    /// <summary>Converte um DateTime UTC para horário de Brasília.</summary>
    public static DateTime ToBrazilTime(this DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), BrazilTz);

    /// <summary>Converte um DateTime? UTC para horário de Brasília (retorna null se null).</summary>
    public static DateTime? ToBrazilTime(this DateTime? utc)
        => utc.HasValue ? utc.Value.ToBrazilTime() : null;
}
