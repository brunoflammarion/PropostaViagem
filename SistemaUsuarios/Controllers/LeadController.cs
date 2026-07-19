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
                .Where(l => l.UsuarioId == masterId && !l.IsDeleted)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            ViewBag.TotalLeads = leads.Count;
            ViewBag.LeadsNovos = leads.Count(l => l.Status == LeadStatus.Novo);

            return View(leads);
        }

        // GET /Lead/Detalhe/{id}
        public async Task<IActionResult> Detalhe(Guid id)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var lead = await _context.Leads
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);

            if (lead == null) return NotFound();

            var historico = await _context.LeadHistoricos
                .Where(h => h.LeadId == id)
                .OrderByDescending(h => h.DataHora)
                .ToListAsync();

            return View(new LeadDetalheViewModel { Lead = lead, Historico = historico });
        }

        // GET /Lead/Editar/{id}
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var lead = await _context.Leads
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);

            if (lead == null) return NotFound();

            var vm = new LeadEditarViewModel
            {
                Id                      = lead.Id,
                FullName                = lead.FullName,
                WhatsApp                = lead.WhatsApp,
                Destination             = lead.Destination,
                Email                   = lead.Email,
                OriginCity              = lead.OriginCity,
                TravelDates             = lead.TravelDates,
                Adults                  = lead.Adults,
                Children                = lead.Children,
                Budget                  = lead.Budget,
                TripType                = lead.TripType,
                AccommodationPreference = lead.AccommodationPreference,
                Notes                   = lead.Notes,
                BestContactTime         = lead.BestContactTime,
                Status                  = lead.Status,
            };

            return View(vm);
        }

        // POST /Lead/Editar/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Guid id, LeadEditarViewModel vm)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var usuarioId = ObterUsuarioLogadoId();
            var lead = await _context.Leads
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);

            if (lead == null) return NotFound();

            RegistrarAlteracao(lead, nameof(lead.FullName),    lead.FullName,                vm.FullName,                masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.WhatsApp),    lead.WhatsApp,                vm.WhatsApp,                masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Destination), lead.Destination,             vm.Destination,             masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Email),       lead.Email,                   vm.Email,                   masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.OriginCity),  lead.OriginCity,              vm.OriginCity,              masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.TravelDates), lead.TravelDates,             vm.TravelDates,             masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Adults),      lead.Adults?.ToString(),       vm.Adults?.ToString(),      masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Children),    lead.Children?.ToString(),     vm.Children?.ToString(),    masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Budget),      lead.Budget,                  vm.Budget,                  masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.TripType),    lead.TripType,                vm.TripType,                masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.AccommodationPreference), lead.AccommodationPreference, vm.AccommodationPreference, masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.Notes),       lead.Notes,                   vm.Notes,                   masterId, usuarioId);
            RegistrarAlteracao(lead, nameof(lead.BestContactTime), lead.BestContactTime,     vm.BestContactTime,         masterId, usuarioId);

            if (lead.Status != vm.Status)
            {
                _context.LeadHistoricos.Add(new LeadHistorico
                {
                    LeadId         = lead.Id,
                    AgenciaId      = masterId,
                    UsuarioId      = usuarioId,
                    TipoAcao       = "EdicaoStatus",
                    CampoAlterado  = nameof(lead.Status),
                    ValorAnterior  = lead.Status.ToString(),
                    ValorNovo      = vm.Status.ToString(),
                });
            }

            lead.FullName                = vm.FullName;
            lead.WhatsApp                = vm.WhatsApp;
            lead.Destination             = vm.Destination;
            lead.Email                   = vm.Email;
            lead.OriginCity              = vm.OriginCity;
            lead.TravelDates             = vm.TravelDates;
            lead.Adults                  = vm.Adults;
            lead.Children                = vm.Children;
            lead.Budget                  = vm.Budget;
            lead.TripType                = vm.TripType;
            lead.AccommodationPreference = vm.AccommodationPreference;
            lead.Notes                   = vm.Notes;
            lead.BestContactTime         = vm.BestContactTime;
            lead.Status                  = vm.Status;
            lead.UpdatedAt               = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Lead atualizado com sucesso.";
            return RedirectToAction("Detalhe", new { id });
        }

        // POST /Lead/Excluir/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");

            var masterId = ObterMasterId();
            var usuarioId = ObterUsuarioLogadoId();
            var lead = await _context.Leads
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);

            if (lead == null) return NotFound();

            lead.IsDeleted             = true;
            lead.ExcluidoEm            = DateTime.Now;
            lead.ExcluidoPorUsuarioId  = usuarioId;
            lead.UpdatedAt             = DateTime.Now;

            _context.LeadHistoricos.Add(new LeadHistorico
            {
                LeadId    = lead.Id,
                AgenciaId = masterId,
                UsuarioId = usuarioId,
                TipoAcao  = "Exclusao",
            });

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Lead excluído com sucesso.";
            return RedirectToAction("Index");
        }

        // POST /Lead/PrepararProposta/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrepararProposta(Guid id)
        {
            if (!UsuarioLogado())
                return Json(new { success = false, message = "Sessão expirada." });

            var masterId  = ObterMasterId();
            var usuarioId = ObterUsuarioLogadoId();

            var lead = await _context.Leads
                .FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);

            if (lead == null)
                return Json(new { success = false, message = "Lead não encontrado." });

            // Se já tem proposta, retorna o vínculo existente
            if (lead.PropostaId.HasValue)
            {
                var existe = await _context.Propostas
                    .AnyAsync(p => p.Id == lead.PropostaId.Value && p.UsuarioMasterId == masterId);
                if (existe)
                    return Json(new { success = true, propostaId = lead.PropostaId.Value, jaExistia = true });
            }

            // Localizar cliente existente por e-mail ou WhatsApp normalizado
            Cliente? cliente = null;
            var candidatos = new List<Cliente>();

            if (!string.IsNullOrWhiteSpace(lead.Email))
            {
                candidatos = await _context.Clientes
                    .Where(c => c.UsuarioId == masterId && !c.IsDeleted
                             && c.Email != null
                             && c.Email.ToLower() == lead.Email.ToLower())
                    .ToListAsync();
            }

            if (candidatos.Count == 0 && !string.IsNullOrWhiteSpace(lead.WhatsApp))
            {
                var whatsNorm = NormalizarTelefone(lead.WhatsApp);
                var todos = await _context.Clientes
                    .Where(c => c.UsuarioId == masterId && !c.IsDeleted && c.Telefone != null)
                    .ToListAsync();
                candidatos = todos
                    .Where(c => NormalizarTelefone(c.Telefone!) == whatsNorm)
                    .ToList();
            }

            if (candidatos.Count > 1)
                return Json(new { success = false, message = "Encontramos mais de um cliente com dados semelhantes. Revise o cadastro antes de continuar." });

            // Montar observações gerais da proposta com os dados disponíveis do lead
            var obs = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(lead.TravelDates))          obs.AppendLine($"Período: {lead.TravelDates}");
            if (!string.IsNullOrWhiteSpace(lead.Budget))                obs.AppendLine($"Orçamento: {lead.Budget}");
            if (!string.IsNullOrWhiteSpace(lead.TripType))              obs.AppendLine($"Tipo de viagem: {lead.TripType}");
            if (!string.IsNullOrWhiteSpace(lead.AccommodationPreference)) obs.AppendLine($"Hospedagem: {lead.AccommodationPreference}");
            if (!string.IsNullOrWhiteSpace(lead.OriginCity))            obs.AppendLine($"Cidade de origem: {lead.OriginCity}");
            if (!string.IsNullOrWhiteSpace(lead.BestContactTime))       obs.AppendLine($"Melhor horário para contato: {lead.BestContactTime}");
            if (!string.IsNullOrWhiteSpace(lead.Notes))
            {
                if (obs.Length > 0) obs.AppendLine("---");
                obs.AppendLine(lead.Notes);
            }

            // Determinar UsuarioMasterId para a proposta
            var tipoStr  = HttpContext.Session.GetString("TipoUsuario");
            var isMaster = tipoStr != "Associado";
            Guid propostaMasterId;
            if (isMaster)
                propostaMasterId = usuarioId;
            else if (Guid.TryParse(HttpContext.Session.GetString("UsuarioMasterId"), out var mId))
                propostaMasterId = mId;
            else
                propostaMasterId = usuarioId;

            // Criar ou reutilizar cliente (sem salvar ainda — Id já está em memória via Guid.NewGuid())
            if (candidatos.Count == 1)
            {
                cliente = candidatos[0];
            }
            else
            {
                cliente = new Cliente
                {
                    UsuarioId = masterId,
                    Nome      = lead.FullName,
                    Email     = lead.Email,
                    Telefone  = lead.WhatsApp,
                    Cidade    = lead.OriginCity,
                };
                _context.Clientes.Add(cliente);
            }

            // Criar proposta (Id em memória permite referenciar antes do SaveChanges)
            var proposta = new Proposta
            {
                Titulo               = $"Viagem para {lead.Destination}",
                UsuarioId            = usuarioId,
                UsuarioMasterId      = propostaMasterId,
                UsuarioResponsavelId = usuarioId,
                NumeroPassageiros    = lead.Adults ?? 1,
                NumeroCriancas       = lead.Children ?? 0,
                ClienteId            = cliente.Id,
                ObservacoesGerais    = obs.Length > 0 ? obs.ToString().TrimEnd() : null,
                StatusProposta       = StatusProposta.Rascunho,
                LinkPublicoAtivo     = true,
                DataCriacao          = DateTime.Now,
            };
            _context.Propostas.Add(proposta);

            // Vincular lead
            lead.ClienteId  = cliente.Id;
            lead.PropostaId = proposta.Id;
            lead.Status     = LeadStatus.Convertido;
            lead.UpdatedAt  = DateTime.Now;

            _context.LeadHistoricos.Add(new LeadHistorico
            {
                LeadId        = lead.Id,
                AgenciaId     = masterId,
                UsuarioId     = usuarioId,
                TipoAcao      = "PropostaCriada",
                CampoAlterado = "PropostaId",
                ValorNovo     = proposta.Id.ToString(),
                Observacao    = $"Proposta \"{proposta.Titulo}\" criada a partir deste lead.",
            });

            // SaveChanges único — EF Core envolve em transação implícita (compatível com SqlServerRetryingExecutionStrategy)
            try
            {
                await _context.SaveChangesAsync();
                TempData["Sucesso"]   = $"Proposta criada para {lead.FullName}.";
                TempData["ActiveTab"] = "passageiros";
                return Json(new { success = true, propostaId = proposta.Id, jaExistia = false });
            }
            catch
            {
                return Json(new { success = false, message = "Não foi possível preparar a proposta. Tente novamente." });
            }
        }

        private static string NormalizarTelefone(string tel) =>
            System.Text.RegularExpressions.Regex.Replace(tel, @"[\s\-\(\)\+]", "");

        private void RegistrarAlteracao(Lead lead, string campo, string? anterior, string? novo, Guid agenciaId, Guid usuarioId)
        {
            if (anterior == novo) return;
            _context.LeadHistoricos.Add(new LeadHistorico
            {
                LeadId        = lead.Id,
                AgenciaId     = agenciaId,
                UsuarioId     = usuarioId,
                TipoAcao      = "EdicaoCampo",
                CampoAlterado = campo,
                ValorAnterior = anterior,
                ValorNovo     = novo,
            });
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
            var usuarioId = ObterUsuarioLogadoId();
            var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UsuarioId == masterId && !l.IsDeleted);
            if (lead == null) return NotFound();

            if (lead.Status != status)
            {
                _context.LeadHistoricos.Add(new LeadHistorico
                {
                    LeadId        = lead.Id,
                    AgenciaId     = masterId,
                    UsuarioId     = usuarioId,
                    TipoAcao      = "EdicaoStatus",
                    CampoAlterado = "Status",
                    ValorAnterior = lead.Status.ToString(),
                    ValorNovo     = status.ToString(),
                });
            }

            lead.Status    = status;
            lead.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
