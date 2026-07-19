using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels.PlatformAdmin;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class PlatformAdminController : Controller
    {
        private readonly ApplicationDbContext  _db;
        private readonly PlatformMetricsService _metrics;
        private readonly DemonstracaoService    _demo;

        public PlatformAdminController(ApplicationDbContext db, PlatformMetricsService metrics, DemonstracaoService demo)
        {
            _db      = db;
            _metrics = metrics;
            _demo    = demo;
        }

        private bool AdminLogado() =>
            HttpContext.Session.GetString("IsPlatformAdmin") == "true";

        // ── Auth ──────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            if (AdminLogado()) return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string senha)
        {
            var admin = await _db.AdminsPlataforma
                .FirstOrDefaultAsync(a => a.Email == email && a.Ativo);

            if (admin == null || !BCrypt.Net.BCrypt.Verify(senha, admin.Senha))
            {
                ViewBag.Erro = "Email ou senha incorretos.";
                return View();
            }

            HttpContext.Session.SetString("IsPlatformAdmin", "true");
            HttpContext.Session.SetString("PlatformAdminId", admin.Id.ToString());
            HttpContext.Session.SetString("PlatformAdminNome", admin.Nome);

            return RedirectToAction("Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("IsPlatformAdmin");
            HttpContext.Session.Remove("PlatformAdminId");
            HttpContext.Session.Remove("PlatformAdminNome");
            return RedirectToAction("Login");
        }

        // ── Dashboard ─────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Dashboard";
            return View(await _metrics.GetDashboardMetrics());
        }

        // ── Agências ──────────────────────────────────────────────────────────

        public async Task<IActionResult> Agencias(string? status = null)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Agências";
            return View(await _metrics.GetAgenciasList(status));
        }

        public async Task<IActionResult> Agencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            var vm = await _metrics.GetAgenciaDetail(id);
            if (vm == null) return NotFound();
            ViewData["Title"] = vm.NomeAgencia;
            return View(vm);
        }

        // ── Ações ─────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearAgencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var master = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == TipoUsuario.Master);

            if (master == null) return NotFound();

            master.Status = StatusUsuario.Bloqueado;

            await _db.Usuarios
                .Where(u => u.UsuarioMasterId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, StatusUsuario.Bloqueado));

            await _db.SaveChangesAsync();

            TempData["Mensagem"] = $"Agência \"{master.NomeAgencia ?? master.Nome}\" bloqueada. Todos os logins foram bloqueados.";
            TempData["MensagemTipo"] = "danger";

            return RedirectToAction("Agencia", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesbloquearAgencia(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var master = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == TipoUsuario.Master);

            if (master == null) return NotFound();

            master.Status = StatusUsuario.Ativo;

            await _db.Usuarios
                .Where(u => u.UsuarioMasterId == id && u.Status == StatusUsuario.Bloqueado)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, StatusUsuario.Ativo));

            await _db.SaveChangesAsync();

            TempData["Mensagem"] = $"Agência \"{master.NomeAgencia ?? master.Nome}\" reativada com sucesso.";
            TempData["MensagemTipo"] = "success";

            return RedirectToAction("Agencia", new { id });
        }

        // ── Reaplicar demonstração para agência específica ────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReaplicarDemonstracao(Guid masterId)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            try
            {
                await _demo.ReaplicarPendentesAsync(masterId);
                TempData["Mensagem"]     = "Demonstração reaplicada com sucesso.";
                TempData["MensagemTipo"] = "success";
            }
            catch (Exception ex)
            {
                TempData["Mensagem"]     = $"Erro ao reaplicar: {ex.Message}";
                TempData["MensagemTipo"] = "danger";
            }

            return RedirectToAction("Agencia", new { id = masterId });
        }

        // ── Consumo de IA ─────────────────────────────────────────────────────

        public async Task<IActionResult> AiUsage(int? ano, int? mes)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            var now = DateTime.UtcNow;
            var a = ano ?? now.Year;
            var m = mes ?? now.Month;
            var periodoInicio = new DateTime(a, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodoFim    = periodoInicio.AddMonths(1);

            var records = await _db.AiUsageRecords
                .AsNoTracking()
                .Where(r => r.DataHoraInicio >= periodoInicio && r.DataHoraInicio < periodoFim)
                .ToListAsync();

            var agenciaIds = records.Select(r => r.AgenciaId).Distinct().ToList();
            var nomes = await _db.Usuarios
                .AsNoTracking()
                .Where(u => agenciaIds.Contains(u.Id))
                .Select(u => new { u.Id, Nome = u.NomeAgencia ?? u.Nome })
                .ToDictionaryAsync(u => u.Id, u => u.Nome);

            var limites = await _db.AiAgencyLimits
                .AsNoTracking()
                .Where(l => agenciaIds.Contains(l.AgenciaId))
                .ToDictionaryAsync(l => l.AgenciaId);

            var vm = new AiUsageGlobalViewModel
            {
                Ano = a, Mes = m,
                CustoTotalUsd   = records.Where(r => r.Sucesso).Sum(r => r.CustoTotal),
                TotalChamadas   = records.Count,
                TotalTokens     = records.Sum(r => (long)r.TotalTokens),
                AgenciasAtivas  = agenciaIds.Count,
                ChamadasBloqueadas = records.Count(r => r.Status == "Blocked"),
                Agencias = agenciaIds.Select(id => new AiUsageAgenciaRow
                {
                    AgenciaId   = id,
                    NomeAgencia = nomes.GetValueOrDefault(id, id.ToString()[..8]),
                    CustoUsd    = records.Where(r => r.AgenciaId == id && r.Sucesso).Sum(r => r.CustoTotal),
                    Chamadas    = records.Count(r => r.AgenciaId == id),
                    Tokens      = records.Where(r => r.AgenciaId == id).Sum(r => (long)r.TotalTokens),
                    LimiteUsd   = limites.TryGetValue(id, out var l) ? l.LimiteMensalCusto : null,
                    ModoControle = limites.TryGetValue(id, out var l2) ? l2.ModoControle : AiModoControle.Monitoramento
                }).OrderByDescending(r => r.CustoUsd).ToList(),
                PorFuncionalidade = records.GroupBy(r => r.Funcionalidade)
                    .Select(g => new AiUsageFuncRow
                    {
                        Funcionalidade = g.Key,
                        Chamadas = g.Count(),
                        CustoUsd = g.Where(r => r.Sucesso).Sum(r => r.CustoTotal),
                        Tokens = g.Sum(r => (long)r.TotalTokens)
                    }).OrderByDescending(r => r.CustoUsd).ToList(),
                PorModelo = records.GroupBy(r => r.Modelo)
                    .Select(g => new AiUsageModeloRow
                    {
                        Modelo = g.Key,
                        Chamadas = g.Count(),
                        CustoUsd = g.Where(r => r.Sucesso).Sum(r => r.CustoTotal)
                    }).OrderByDescending(r => r.CustoUsd).ToList()
            };

            ViewData["Title"] = $"Consumo IA — {vm.MesLabel}";
            return View(vm);
        }

        public async Task<IActionResult> AiUsageAgencia(Guid id, int? ano, int? mes, int pagina = 1)
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            var now = DateTime.UtcNow;
            var a = ano ?? now.Year;
            var m = mes ?? now.Month;
            var periodoInicio = new DateTime(a, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodoFim    = periodoInicio.AddMonths(1);

            var agencia = await _db.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == TipoUsuario.Master);
            if (agencia == null) return NotFound();

            var limite = await _db.AiAgencyLimits.AsNoTracking()
                .FirstOrDefaultAsync(l => l.AgenciaId == id && l.Ativo);

            var query = _db.AiUsageRecords.AsNoTracking()
                .Where(r => r.AgenciaId == id && r.DataHoraInicio >= periodoInicio && r.DataHoraInicio < periodoFim);

            var records = await query.ToListAsync();
            var total = await query.CountAsync();

            var historico = await query
                .OrderByDescending(r => r.DataHoraInicio)
                .Skip((pagina - 1) * AiUsageAgenciaDetalheViewModel.PageSize)
                .Take(AiUsageAgenciaDetalheViewModel.PageSize)
                .ToListAsync();

            var vm = new AiUsageAgenciaDetalheViewModel
            {
                AgenciaId = id,
                NomeAgencia = agencia.NomeAgencia ?? agencia.Nome,
                Ano = a, Mes = m,
                CustoMesUsd = records.Where(r => r.Sucesso).Sum(r => r.CustoTotal),
                ChamadasMes = records.Count,
                TokensMes = records.Sum(r => (long)r.TotalTokens),
                ChamadasBloqueadas = records.Count(r => r.Status == "Blocked"),
                Limite = limite == null ? new AiLimiteFormViewModel { AgenciaId = id } : new AiLimiteFormViewModel
                {
                    AgenciaId = id,
                    LimiteMensalCusto = limite.LimiteMensalCusto,
                    ModoControle = limite.ModoControle,
                    PercentualAlerta = limite.PercentualAlerta,
                    PermitirExcedente = limite.PermitirExcedente,
                    ValorExcedentePermitido = limite.ValorExcedentePermitido
                },
                PorFuncionalidade = records.GroupBy(r => r.Funcionalidade)
                    .Select(g => new AiUsageFuncRow
                    {
                        Funcionalidade = g.Key,
                        Chamadas = g.Count(),
                        CustoUsd = g.Where(r => r.Sucesso).Sum(r => r.CustoTotal),
                        Tokens = g.Sum(r => (long)r.TotalTokens)
                    }).OrderByDescending(r => r.CustoUsd).ToList(),
                Historico = historico.Select(r => new AiUsageRecordRow
                {
                    DataHora = r.DataHoraInicio,
                    Funcionalidade = r.Funcionalidade,
                    Modelo = r.Modelo,
                    TotalTokens = r.TotalTokens,
                    CustoUsd = r.CustoTotal,
                    Sucesso = r.Sucesso,
                    Status = r.Status,
                    DuracaoMs = r.DuracaoMs
                }).ToList(),
                TotalHistorico = total,
                PaginaAtual = pagina
            };

            ViewData["Title"] = $"IA — {vm.NomeAgencia}";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLimiteAgencia(AiLimiteFormViewModel form)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var adminIdStr = HttpContext.Session.GetString("PlatformAdminId");
            var adminId = adminIdStr != null ? Guid.Parse(adminIdStr) : Guid.Empty;

            var limite = await _db.AiAgencyLimits
                .FirstOrDefaultAsync(l => l.AgenciaId == form.AgenciaId && l.Ativo);

            if (limite == null)
            {
                limite = new AiAgencyLimit { AgenciaId = form.AgenciaId, CriadoEm = DateTime.UtcNow };
                _db.AiAgencyLimits.Add(limite);
            }

            // Registrar auditoria de cada campo alterado
            void Audit(string campo, string? anterior, string? novo)
            {
                if (anterior == novo) return;
                _db.AiLimitAuditLogs.Add(new AiLimitAuditLog
                {
                    AgenciaId = form.AgenciaId,
                    AdministradorId = adminId,
                    Campo = campo,
                    ValorAnterior = anterior,
                    ValorNovo = novo,
                    Motivo = form.Motivo,
                    DataHora = DateTime.UtcNow
                });
            }

            Audit("LimiteMensalCusto",  limite.LimiteMensalCusto?.ToString(), form.LimiteMensalCusto?.ToString());
            Audit("ModoControle",        limite.ModoControle.ToString(),       form.ModoControle.ToString());
            Audit("PercentualAlerta",    limite.PercentualAlerta.ToString(),   form.PercentualAlerta.ToString());
            Audit("PermitirExcedente",   limite.PermitirExcedente.ToString(),  form.PermitirExcedente.ToString());
            Audit("ValorExcedentePermitido", limite.ValorExcedentePermitido?.ToString(), form.ValorExcedentePermitido?.ToString());

            limite.LimiteMensalCusto         = form.LimiteMensalCusto;
            limite.ModoControle              = form.ModoControle;
            limite.PercentualAlerta          = form.PercentualAlerta;
            limite.PermitirExcedente         = form.PermitirExcedente;
            limite.ValorExcedentePermitido   = form.ValorExcedentePermitido;
            limite.AtualizadoEm              = DateTime.UtcNow;
            limite.AtualizadoPorAdminId      = adminId;

            await _db.SaveChangesAsync();

            TempData["Mensagem"]     = "Limite de IA atualizado com sucesso.";
            TempData["MensagemTipo"] = "success";

            return RedirectToAction("AiUsageAgencia", new { id = form.AgenciaId });
        }

        // ── Conteúdos de Demonstração ─────────────────────────────────────────

        public async Task<IActionResult> Demonstracao()
        {
            if (!AdminLogado()) return RedirectToAction("Login");
            ViewData["Title"] = "Conteúdos de Demonstração";

            var modelos = await _db.ConteudosDemonstracao
                .Include(c => c.CriadoPorAdmin)
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            // Enriquecer com dados das entidades de origem (nome + tipo)
            var vm = new List<ConteudoDemonstracaoItemVm>();
            foreach (var m in modelos)
            {
                string nomeEntidade = "(não encontrada)";
                string? fotoCapa    = null;

                if (m.TipoConteudo == TipoConteudoDemonstracao.Proposta)
                {
                    var p = await _db.Propostas.AsNoTracking()
                        .Where(x => x.Id == m.EntidadeOrigemId)
                        .Select(x => new { x.Titulo, x.FotoCapa })
                        .FirstOrDefaultAsync();
                    if (p != null) { nomeEntidade = p.Titulo; fotoCapa = p.FotoCapa; }
                }
                else
                {
                    var o = await _db.Ofertas.AsNoTracking()
                        .Where(x => x.Id == m.EntidadeOrigemId)
                        .Select(x => new { x.Nome, x.ImagemPrincipalPath })
                        .FirstOrDefaultAsync();
                    if (o != null) { nomeEntidade = o.Nome; fotoCapa = o.ImagemPrincipalPath; }
                }

                var totalAplicados = await _db.ConteudosDemonstracaoAplicados
                    .CountAsync(a => a.ConteudoDemonstracaoId == m.Id
                               && a.StatusAplicacao == StatusAplicacaoDemonstracao.Sucesso);

                vm.Add(new ConteudoDemonstracaoItemVm
                {
                    Modelo        = m,
                    NomeEntidade  = nomeEntidade,
                    FotoCapa      = fotoCapa,
                    TotalAplicados= totalAplicados,
                });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarConteudo(TipoConteudoDemonstracao tipo, Guid entidadeId, string? nomeAdmin)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var adminId = Guid.Parse(HttpContext.Session.GetString("PlatformAdminId")!);

            // Validar que a entidade existe
            bool existe = tipo == TipoConteudoDemonstracao.Proposta
                ? await _db.Propostas.AnyAsync(p => p.Id == entidadeId)
                : await _db.Ofertas.AnyAsync(o => o.Id == entidadeId);

            if (!existe)
            {
                TempData["Mensagem"]     = "Entidade não encontrada.";
                TempData["MensagemTipo"] = "danger";
                return RedirectToAction("Demonstracao");
            }

            // Evitar duplicata
            var jaExiste = await _db.ConteudosDemonstracao
                .AnyAsync(c => c.EntidadeOrigemId == entidadeId && c.TipoConteudo == tipo);
            if (jaExiste)
            {
                TempData["Mensagem"]     = "Este conteúdo já está na lista de demonstração.";
                TempData["MensagemTipo"] = "warning";
                return RedirectToAction("Demonstracao");
            }

            var maxOrdem = await _db.ConteudosDemonstracao.MaxAsync(c => (int?)c.Ordem) ?? 0;

            _db.ConteudosDemonstracao.Add(new ConteudoDemonstracao
            {
                TipoConteudo          = tipo,
                EntidadeOrigemId      = entidadeId,
                NomeAdministrativo    = string.IsNullOrWhiteSpace(nomeAdmin) ? null : nomeAdmin.Trim(),
                Ativo                 = true,
                AplicarAutomaticamente= true,
                Ordem                 = maxOrdem + 1,
                DataCriacao           = DateTime.Now,
                CriadoPorAdminId      = adminId,
            });
            await _db.SaveChangesAsync();

            TempData["Mensagem"]     = "Conteúdo adicionado ao catálogo de demonstração.";
            TempData["MensagemTipo"] = "success";
            return RedirectToAction("Demonstracao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleConteudo(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var c = await _db.ConteudosDemonstracao.FindAsync(id);
            if (c == null) return NotFound();

            c.Ativo           = !c.Ativo;
            c.DataAtualizacao = DateTime.Now;
            c.AtualizadoPorAdminId = Guid.Parse(HttpContext.Session.GetString("PlatformAdminId")!);
            await _db.SaveChangesAsync();

            return RedirectToAction("Demonstracao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverConteudo(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var c = await _db.ConteudosDemonstracao.FindAsync(id);
            if (c == null) return NotFound();

            _db.ConteudosDemonstracao.Remove(c);
            await _db.SaveChangesAsync();

            TempData["Mensagem"]     = "Conteúdo removido do catálogo.";
            TempData["MensagemTipo"] = "success";
            return RedirectToAction("Demonstracao");
        }

        // ── Lista VIP ─────────────────────────────────────────────────────────

        public async Task<IActionResult> ListaVip(
            string? busca, string? periodo, string? propostas, string? ordem, int pagina = 1)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var hoje = DateTime.Today;

            var totalGeral = await _db.ListaVipCadastros.CountAsync();
            var totalHoje  = await _db.ListaVipCadastros.CountAsync(c => c.DataCadastro >= hoje);
            var total7d    = await _db.ListaVipCadastros.CountAsync(c => c.DataCadastro >= hoje.AddDays(-7));
            var total30d   = await _db.ListaVipCadastros.CountAsync(c => c.DataCadastro >= hoje.AddDays(-30));
            var totalNovos = await _db.ListaVipCadastros.CountAsync(c => !c.Visualizado);

            var query = _db.ListaVipCadastros.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var b = busca.Trim().ToLower();
                query = query.Where(c =>
                    c.Nome.ToLower().Contains(b) ||
                    c.Email.ToLower().Contains(b) ||
                    c.NomeAgencia.ToLower().Contains(b) ||
                    c.Whatsapp.Contains(b) ||
                    (c.Cidade != null && c.Cidade.ToLower().Contains(b)) ||
                    (c.Instagram != null && c.Instagram.ToLower().Contains(b)));
            }

            if (!string.IsNullOrWhiteSpace(periodo))
            {
                DateTime? cutoff = periodo switch
                {
                    "hoje" => hoje,
                    "7d"   => hoje.AddDays(-7),
                    "30d"  => hoje.AddDays(-30),
                    "90d"  => hoje.AddDays(-90),
                    _      => null
                };
                if (cutoff.HasValue) query = query.Where(c => c.DataCadastro >= cutoff.Value);
            }

            if (!string.IsNullOrWhiteSpace(propostas))
                query = query.Where(c => c.PropostasPorMes == propostas);

            List<SistemaUsuarios.Models.ListaVipCadastro> cadastros;
            int totalFiltrado;

            if (ordem == "propostas")
            {
                // Ordenação por propostas exige sort in-memory (valor categórico)
                var todos = await query.ToListAsync();
                totalFiltrado = todos.Count;
                cadastros = todos
                    .OrderByDescending(c => ListaVipListaViewModel.PropostasOrdem(c.PropostasPorMes))
                    .Skip((pagina - 1) * ListaVipListaViewModel.PageSize)
                    .Take(ListaVipListaViewModel.PageSize)
                    .ToList();
            }
            else
            {
                query = ordem switch
                {
                    "antigos" => query.OrderBy(c => c.DataCadastro),
                    "nome"    => query.OrderBy(c => c.Nome),
                    _         => query.OrderByDescending(c => c.DataCadastro)
                };
                totalFiltrado = await query.CountAsync();
                cadastros = await query
                    .Skip((pagina - 1) * ListaVipListaViewModel.PageSize)
                    .Take(ListaVipListaViewModel.PageSize)
                    .ToListAsync();
            }

            var vm = new ListaVipListaViewModel
            {
                Cadastros       = cadastros,
                TotalGeral      = totalGeral,
                TotalHoje       = totalHoje,
                Total7d         = total7d,
                Total30d        = total30d,
                TotalNovos      = totalNovos,
                TotalFiltrado   = totalFiltrado,
                PaginaAtual     = pagina,
                TotalPaginas    = (int)Math.Ceiling((double)totalFiltrado / ListaVipListaViewModel.PageSize),
                Busca           = busca,
                Periodo         = periodo,
                FiltroPropostas = propostas,
                Ordem           = ordem,
            };

            ViewData["Title"]        = "Lista VIP";
            ViewBag.ListaVipNovos    = totalNovos;
            return View(vm);
        }

        public async Task<IActionResult> ListaVipDetalhe(Guid id)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var cadastro = await _db.ListaVipCadastros.FindAsync(id);
            if (cadastro == null) return NotFound();

            if (!cadastro.Visualizado)
            {
                cadastro.Visualizado     = true;
                cadastro.DataVisualizacao = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            ViewData["Title"]     = cadastro.Nome;
            ViewData["ActiveNav"] = "ListaVip";
            ViewData["Breadcrumb"]= "<a href='/PlatformAdmin/ListaVip'>Lista VIP</a>";
            ViewBag.ListaVipNovos = await _db.ListaVipCadastros.CountAsync(c => !c.Visualizado);
            return View(cadastro);
        }

        public async Task<IActionResult> ExportarListaVip(string? busca, string? periodo, string? propostas)
        {
            if (!AdminLogado()) return RedirectToAction("Login");

            var query = _db.ListaVipCadastros.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var b = busca.Trim().ToLower();
                query = query.Where(c =>
                    c.Nome.ToLower().Contains(b) || c.Email.ToLower().Contains(b) ||
                    c.NomeAgencia.ToLower().Contains(b) ||
                    (c.Cidade != null && c.Cidade.ToLower().Contains(b)));
            }
            if (!string.IsNullOrWhiteSpace(periodo))
            {
                DateTime? cutoff = periodo switch { "hoje" => DateTime.Today, "7d" => DateTime.Today.AddDays(-7), "30d" => DateTime.Today.AddDays(-30), "90d" => DateTime.Today.AddDays(-90), _ => null };
                if (cutoff.HasValue) query = query.Where(c => c.DataCadastro >= cutoff.Value);
            }
            if (!string.IsNullOrWhiteSpace(propostas))
                query = query.Where(c => c.PropostasPorMes == propostas);

            var dados = await query.OrderByDescending(c => c.DataCadastro).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Nome,Email,WhatsApp,Agência,Cidade,Instagram,Propostas/mês,Origem,Data");
            foreach (var c in dados)
            {
                static string Esc(string? v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";
                sb.AppendLine($"{Esc(c.Nome)},{Esc(c.Email)},{Esc(c.Whatsapp)},{Esc(c.NomeAgencia)},{Esc(c.Cidade)},{Esc(c.Instagram)},{Esc(ListaVipListaViewModel.PropostasLabel(c.PropostasPorMes))},{Esc(c.Origem)},{Esc(c.DataCadastro.ToString("dd/MM/yyyy HH:mm"))}");
            }

            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var content  = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var bytes    = preamble.Concat(content).ToArray();
            return File(bytes, "text/csv", $"lista-vip-{DateTime.Today:yyyy-MM-dd}.csv");
        }

        // AJAX: preview de entidade antes de adicionar
        [HttpGet]
        public async Task<IActionResult> BuscarEntidade(string tipo, Guid id)
        {
            if (!AdminLogado()) return Unauthorized();

            if (tipo == "Proposta")
            {
                var p = await _db.Propostas.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new { x.Id, x.Titulo, x.FotoCapa, x.StatusProposta, x.DataCriacao,
                                       DestinCount = x.Destinos.Count })
                    .FirstOrDefaultAsync();
                if (p == null) return Json(new { encontrado = false });
                return Json(new { encontrado = true, tipo = "Proposta",
                    nome  = p.Titulo,
                    foto  = p.FotoCapa,
                    extra = $"{p.DestinCount} destino(s) · {p.StatusProposta}",
                    data  = p.DataCriacao.ToString("dd/MM/yyyy") });
            }
            else
            {
                var o = await _db.Ofertas.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new { x.Id, x.Nome, x.ImagemPrincipalPath, x.TemplateId, x.DataCriacao })
                    .FirstOrDefaultAsync();
                if (o == null) return Json(new { encontrado = false });
                return Json(new { encontrado = true, tipo = "Oferta",
                    nome  = o.Nome,
                    foto  = o.ImagemPrincipalPath,
                    extra = $"Template: {o.TemplateId}",
                    data  = o.DataCriacao.ToString("dd/MM/yyyy") });
            }
        }
    }

    // ViewModel auxiliar para a tela de demonstração
    public class ConteudoDemonstracaoItemVm
    {
        public ConteudoDemonstracao Modelo        { get; set; } = null!;
        public string               NomeEntidade  { get; set; } = "";
        public string?              FotoCapa      { get; set; }
        public int                  TotalAplicados{ get; set; }
    }
}
