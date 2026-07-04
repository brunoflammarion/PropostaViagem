using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;

namespace SistemaUsuarios.Controllers
{
    public class LeadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LeadController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        // Retorna o ID do master (para usuários Associado, é o UsuarioMasterId; para Master, é o próprio Id)
        private Guid ObterMasterId()
        {
            var tipo = HttpContext.Session.GetString("TipoUsuario");
            if (tipo == "Associado")
            {
                var masterId = HttpContext.Session.GetString("UsuarioMasterId");
                if (masterId != null) return Guid.Parse(masterId);
            }
            return ObterUsuarioLogadoId();
        }

        private string BuildPublicUrl(string? slug)
        {
            if (string.IsNullOrEmpty(slug)) return "";
            var req = HttpContext.Request;
            return $"{req.Scheme}://{req.Host}/{slug}";
        }

        // GET /Lead  →  lista de leads recebidos
        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var leads = await _context.Leads
                .Where(l => l.UsuarioId == masterId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            ViewBag.TotalLeads = leads.Count;
            ViewBag.LeadsNovos = leads.Count(l => l.Status == LeadStatus.Novo);

            return View(leads);
        }

        // GET /Lead/Captacao  →  tela de configuração de captação
        public async Task<IActionResult> Captacao()
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var usuario = await _context.Usuarios.FindAsync(masterId);
            if (usuario == null) return RedirectToAction("Logout", "Auth");

            var settings = await _context.LeadCaptureSettings
                .FirstOrDefaultAsync(s => s.UsuarioId == masterId);

            var vm = new LeadCaptacaoViewModel
            {
                NomeAgencia   = usuario.NomeAgencia,
                SlugAgencia   = usuario.SlugAgencia,
                PublicUrl     = BuildPublicUrl(usuario.SlugAgencia),
                IsActive      = settings?.IsActive ?? false,
                WelcomeMessage        = settings?.WelcomeMessage,
                ResponseTimeText      = settings?.ResponseTimeText,
                ShowEmail                   = settings?.ShowEmail ?? true,
                ShowOriginCity              = settings?.ShowOriginCity ?? false,
                ShowTravelDates             = settings?.ShowTravelDates ?? false,
                ShowAdults                  = settings?.ShowAdults ?? false,
                ShowChildren                = settings?.ShowChildren ?? false,
                ShowBudget                  = settings?.ShowBudget ?? false,
                ShowTripType                = settings?.ShowTripType ?? false,
                ShowAccommodationPreference = settings?.ShowAccommodationPreference ?? false,
                ShowNotes                   = settings?.ShowNotes ?? false,
                ShowBestContactTime         = settings?.ShowBestContactTime ?? false,
                SettingsId    = settings?.Id ?? Guid.Empty,
                TotalLeads    = await _context.Leads.CountAsync(l => l.UsuarioId == masterId),
                LeadsNovos    = await _context.Leads.CountAsync(l => l.UsuarioId == masterId && l.Status == LeadStatus.Novo),
            };

            return View(vm);
        }

        // POST /Lead/Captacao  →  salva configurações
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Captacao(LeadCaptacaoViewModel vm)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var usuario = await _context.Usuarios.FindAsync(masterId);
            if (usuario == null) return RedirectToAction("Logout", "Auth");

            // Validação: para ativar, precisa de nome da agência e tempo de resposta
            if (vm.IsActive)
            {
                if (string.IsNullOrWhiteSpace(usuario.NomeAgencia))
                {
                    TempData["ErroCaptacao"] = "Configure o nome da sua agência antes de ativar a captação.";
                    vm.NomeAgencia = usuario.NomeAgencia;
                    vm.SlugAgencia = usuario.SlugAgencia;
                    vm.PublicUrl   = BuildPublicUrl(usuario.SlugAgencia);
                    vm.TotalLeads  = await _context.Leads.CountAsync(l => l.UsuarioId == masterId);
                    vm.LeadsNovos  = await _context.Leads.CountAsync(l => l.UsuarioId == masterId && l.Status == LeadStatus.Novo);
                    return View(vm);
                }

                if (string.IsNullOrWhiteSpace(vm.ResponseTimeText))
                {
                    TempData["ErroCaptacao"] = "Informe o tempo de resposta antes de ativar a captação.";
                    vm.NomeAgencia = usuario.NomeAgencia;
                    vm.SlugAgencia = usuario.SlugAgencia;
                    vm.PublicUrl   = BuildPublicUrl(usuario.SlugAgencia);
                    vm.TotalLeads  = await _context.Leads.CountAsync(l => l.UsuarioId == masterId);
                    vm.LeadsNovos  = await _context.Leads.CountAsync(l => l.UsuarioId == masterId && l.Status == LeadStatus.Novo);
                    return View(vm);
                }
            }

            var settings = await _context.LeadCaptureSettings
                .FirstOrDefaultAsync(s => s.UsuarioId == masterId);

            if (settings == null)
            {
                settings = new LeadCaptureSettings { UsuarioId = masterId };
                _context.LeadCaptureSettings.Add(settings);
            }

            settings.IsActive                   = vm.IsActive;
            settings.WelcomeMessage             = vm.WelcomeMessage?.Trim();
            settings.ResponseTimeText           = vm.ResponseTimeText?.Trim();
            settings.ShowEmail                  = vm.ShowEmail;
            settings.ShowOriginCity             = vm.ShowOriginCity;
            settings.ShowTravelDates            = vm.ShowTravelDates;
            settings.ShowAdults                 = vm.ShowAdults;
            settings.ShowChildren               = vm.ShowChildren;
            settings.ShowBudget                 = vm.ShowBudget;
            settings.ShowTripType               = vm.ShowTripType;
            settings.ShowAccommodationPreference = vm.ShowAccommodationPreference;
            settings.ShowNotes                  = vm.ShowNotes;
            settings.ShowBestContactTime        = vm.ShowBestContactTime;
            settings.UpdatedAt                  = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SucessoCaptacao"] = "Configurações salvas com sucesso!";
            return RedirectToAction("Captacao");
        }

        // POST /Lead/AlterarStatus  →  atualiza status de um lead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarStatus(Guid id, LeadStatus status)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId);
            if (lead == null) return NotFound();

            lead.Status = status;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
