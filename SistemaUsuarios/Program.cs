using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configurar Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar sessões
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Adicionar HttpContextAccessor para acessar o contexto HTTP
builder.Services.AddHttpContextAccessor();

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

// Usar sessões
app.UseSession();

app.UseAuthorization();

// Configurar rotas
app.MapControllerRoute(
    name: "analytics",
    pattern: "PropostaAnalyticsData/PropostaDetalhada/{propostaId}",
    defaults: new { controller = "PropostaAnalyticsData", action = "PropostaDetalhada" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Adicionar rota específica para landing page
app.MapControllerRoute(
    name: "landing",
    pattern: "cadastro-agentes",
    defaults: new { controller = "Landing", action = "Index" });

app.MapControllerRoute(
    name: "landing_default",
    pattern: "landing/{action=Index}/{id?}",
    defaults: new { controller = "Landing" });

// Garantir que o banco seja criado e populado
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Aplicar migrações pendentes
        context.Database.Migrate();

        // Seed layouts se não existirem
        if (!context.Layouts.Any())
        {
            context.Layouts.AddRange(
                new SistemaUsuarios.Models.Layout
                {
                    Nome = "Layout Padrão",
                    Descricao = "Layout padrão do sistema",
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
                    Descricao = "Layout para viagens em família",
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