using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Services;
using SistemaUsuarios.Infrastructure;
using NetTopologySuite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configurar Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions
            .UseNetTopologySuite()
            .CommandTimeout(120)
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)
    )
);

// Configurar sess�es
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Adicionar HttpContextAccessor para acessar o contexto HTTP
builder.Services.AddHttpContextAccessor();

// Cache em memória para rate limiting de tentativas de acesso
builder.Services.AddMemoryCache();

// AeroDataBox flight lookup (via API.Market)
builder.Services.AddHttpClient<IFlightLookupService, AeroDataBoxService>();

// AI Copilot
builder.Services.AddHttpClient<AiCopilotService>();

// Importação Inteligente
builder.Services.AddHttpClient<ImportacaoIAService>();

// Google Places — timeout de 12s (autocomplete + detalhes de hotel/destino)
builder.Services.AddHttpClient("GooglePlaces", client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
});

// OpenAI — timeout de 45s (geração de descrição via GPT)
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<ImportacaoPersistenciaService>();

// Armazenamento de arquivos (Azure Blob Storage)
builder.Services.AddSingleton<SistemaUsuarios.Services.BlobStorageService>();

// Módulo Tarefas
builder.Services.AddScoped<ITarefaService, TarefaService>();

// Platform Admin
builder.Services.AddScoped<SistemaUsuarios.Services.PlatformMetricsService>();

// Conteúdos de Demonstração
builder.Services.AddScoped<SistemaUsuarios.Services.DemonstracaoService>();

// Gateway central de IA (todas as chamadas ao provedor passam por aqui)
builder.Services.AddScoped<IAiGatewayService, AiGatewayService>();

// Cálculo de idade e categoria de passageiro na data da viagem
builder.Services.AddScoped<IPassengerAgeService, PassengerAgeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Usar sess�es
app.UseSession();

app.UseAuthorization();

// Configurar rotas
app.MapControllerRoute(
    name: "analytics",
    pattern: "PropostaAnalyticsData/PropostaDetalhada/{propostaId}",
    defaults: new { controller = "PropostaAnalyticsData", action = "PropostaDetalhada" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "produto",
    pattern: "produto",
    defaults: new { controller = "Landing", action = "Produto" });

// Rota legada da landing de produto
app.MapControllerRoute(
    name: "landing",
    pattern: "cadastro-agentes",
    defaults: new { controller = "Landing", action = "Produto" });

app.MapControllerRoute(
    name: "landing_default",
    pattern: "landing/{action=Index}/{id?}",
    defaults: new { controller = "Landing" });

// ── Rota pública por slug de agência ─────────────────────────────────────
// DEVE ser a ÚLTIMA rota registrada. A constraint garante que não conflita
// com nenhum controller ou prefixo de rota interno do sistema.
// Health check — usado pelo Azure e monitoramento externo
app.MapGet("/_health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllerRoute(
    name: "agency-public",
    pattern: "{agencySlug}",
    defaults: new { controller = "AgencyPublic", action = "Index" },
    constraints: new { agencySlug = new NotReservedSlugConstraint() });

// Garantir que o banco seja criado e populado
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Aplicar migra��es pendentes
        context.Database.Migrate();

        // Seed: primeiro admin da plataforma
        if (!context.AdminsPlataforma.Any())
        {
            context.AdminsPlataforma.Add(new SistemaUsuarios.Models.AdminPlataforma
            {
                Nome  = "Bruno",
                Email = "bruno.tromp@gmail.com",
                Senha = BCrypt.Net.BCrypt.HashPassword("Admin@2025!"),
                Ativo = true,
                DataCriacao = DateTime.Now,
            });
            context.SaveChanges();
        }

        // Seed layouts se n�o existirem
        if (!context.Layouts.Any())
        {
            context.Layouts.AddRange(
                new SistemaUsuarios.Models.Layout
                {
                    Nome = "Layout Padr�o",
                    Descricao = "Layout padr�o do sistema",
                    Ativo = true,
                    DataCriacao = DateTime.Now
                },
                new SistemaUsuarios.Models.Layout
                {
                    Nome = "Layout Executivo",
                    Descricao = "Layout para viagens executivas",
                    Ativo = true,
                    DataCriacao = DateTime.Now
                },
                new SistemaUsuarios.Models.Layout
                {
                    Nome = "Layout Familiar",
                    Descricao = "Layout para viagens em fam�lia",
                    Ativo = true,
                    DataCriacao = DateTime.Now
                }
            );
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        // Log do erro
        Console.WriteLine($"Erro ao inicializar banco de dados: {ex.Message}");
    }
}

app.Run();