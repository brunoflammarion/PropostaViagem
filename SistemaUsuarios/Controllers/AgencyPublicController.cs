using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Controllers
{
    public class AgencyPublicController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AgencyPublicController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET /{agencySlug}
        public async Task<IActionResult> Index(string agencySlug)
        {
            if (string.IsNullOrWhiteSpace(agencySlug))
                return NotFound();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.SlugAgencia != null &&
                                          u.SlugAgencia.ToLower() == agencySlug.ToLower() &&
                                          u.TipoUsuario == TipoUsuario.Master);

            if (usuario == null)
                return NotFound();

            var settings = await _context.LeadCaptureSettings
                .FirstOrDefaultAsync(s => s.UsuarioId == usuario.Id);

            var vm = new AgencyPublicViewModel
            {
                NomeAgencia   = usuario.NomeAgencia ?? usuario.Nome,
                SlugAgencia   = usuario.SlugAgencia!,
                FotoPath      = usuario.FotoPath,
                CorPrimaria   = usuario.CorPrimaria,
                CorSecundaria = usuario.CorSecundaria,
                CorDestaque   = usuario.CorDestaque,
                CaptacaoAtiva = settings?.IsActive ?? false,
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
            };

            return View(vm);
        }

        // POST /{agencySlug}  →  recebe o lead público
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string agencySlug,
            string fullName, string whatsApp, string destination,
            string? email, string? originCity, string? travelDates,
            int? adults, int? children, string? budget,
            string? tripType, string? accommodationPreference,
            string? notes, string? bestContactTime)
        {
            if (string.IsNullOrWhiteSpace(agencySlug))
                return NotFound();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.SlugAgencia != null &&
                                          u.SlugAgencia.ToLower() == agencySlug.ToLower() &&
                                          u.TipoUsuario == TipoUsuario.Master);

            if (usuario == null)
                return NotFound();

            var settings = await _context.LeadCaptureSettings
                .FirstOrDefaultAsync(s => s.UsuarioId == usuario.Id);

            var vm = new AgencyPublicViewModel
            {
                NomeAgencia   = usuario.NomeAgencia ?? usuario.Nome,
                SlugAgencia   = usuario.SlugAgencia!,
                FotoPath      = usuario.FotoPath,
                CorPrimaria   = usuario.CorPrimaria,
                CorSecundaria = usuario.CorSecundaria,
                CorDestaque   = usuario.CorDestaque,
                CaptacaoAtiva = settings?.IsActive ?? false,
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
            };

            if (!vm.CaptacaoAtiva)
            {
                vm.ErroEnvio = "Esta agência não está recebendo novas solicitações no momento.";
                return View(vm);
            }

            // Validação server-side dos campos obrigatórios
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(whatsApp) ||
                string.IsNullOrWhiteSpace(destination))
            {
                vm.ErroEnvio = "Preencha todos os campos obrigatórios.";
                return View(vm);
            }

            // Sanitiza strings antes de persistir
            var lead = new Lead
            {
                UsuarioId              = usuario.Id,
                FullName               = Sanitize(fullName, 200),
                WhatsApp               = Regex.Replace(whatsApp ?? "", @"[^\d\+\-\(\)\s]", "").Trim()[..Math.Min(20, Regex.Replace(whatsApp ?? "", @"[^\d\+\-\(\)\s]", "").Trim().Length)],
                Destination            = Sanitize(destination, 300),
                Email                  = SanitizeOptional(email, 150),
                OriginCity             = SanitizeOptional(originCity, 150),
                TravelDates            = SanitizeOptional(travelDates, 100),
                Adults                 = adults,
                Children               = children,
                Budget                 = SanitizeOptional(budget, 100),
                TripType               = SanitizeOptional(tripType, 100),
                AccommodationPreference = SanitizeOptional(accommodationPreference, 150),
                Notes                  = SanitizeOptional(notes, 1000),
                BestContactTime        = SanitizeOptional(bestContactTime, 100),
                Source                 = "Landing Page Pública",
                Status                 = LeadStatus.Novo,
                CreatedAt              = DateTime.Now,
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            vm.Enviado = true;
            return View(vm);
        }

        private static string Sanitize(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            // Remove HTML tags
            var clean = Regex.Replace(value.Trim(), @"<[^>]+>", "");
            return clean.Length > maxLen ? clean[..maxLen] : clean;
        }

        private static string? SanitizeOptional(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var clean = Regex.Replace(value.Trim(), @"<[^>]+>", "");
            return clean.Length > maxLen ? clean[..maxLen] : clean;
        }
    }
}
