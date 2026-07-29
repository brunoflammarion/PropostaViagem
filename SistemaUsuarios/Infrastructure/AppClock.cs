namespace SistemaUsuarios.Infrastructure;

/// <summary>
/// Abstração central de data e hora da aplicação.
/// Todas as classes que precisam do "agora" devem usar esta interface,
/// nunca DateTime.Now ou DateTime.Today diretamente.
/// </summary>
public interface IAppClock
{
    /// <summary>Instante atual em UTC. Use para gravar timestamps de sistema.</summary>
    DateTime UtcNow { get; }

    /// <summary>Instante atual no fuso de Brasília. Use para lógica de apresentação e saudações.</summary>
    DateTime BrazilNow { get; }

    /// <summary>Data de hoje no fuso de Brasília (sem componente de hora). Use para comparações de tarefas/agenda.</summary>
    DateTime Today { get; }
}

public sealed class AppClock : IAppClock
{
    private static readonly TimeZoneInfo BrazilTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime BrazilNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrazilTz);

    public DateTime Today => BrazilNow.Date;
}
