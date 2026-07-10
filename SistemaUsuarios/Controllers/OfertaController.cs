using SistemaUsuarios.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;

namespace SistemaUsuarios.Controllers
{
    public class OfertaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blob;

        public OfertaController(ApplicationDbContext context, BlobStorageService blob)
        {
            _context = context;
            _blob = blob;
        }

        // ── Helpers de sessão ─────────────────────────────────────────────────────

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        private Guid ObterMasterIdParaSalvar()
        {
            if (SessaoIsMaster())
                return ObterUsuarioLogadoId();

            var masterIdStr = HttpContext.Session.GetString("UsuarioMasterId");
            return masterIdStr != null ? Guid.Parse(masterIdStr) : ObterUsuarioLogadoId();
        }

        // ── GET /Oferta ───────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var ofertas = await _context.Ofertas
                .Where(o => isMaster
                    ? o.UsuarioMasterId == usuarioId
                    : o.UsuarioId == usuarioId)
                .Include(o => o.Usuario)
                .OrderByDescending(o => o.DataCriacao)
                .AsNoTracking()
                .ToListAsync();

            return View(ofertas);
        }

        // ── GET /Oferta/Criar ─────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Criar()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            PreencherViewBagIdentidade();
            return View("Criar", new OfertaViewModel());
        }

        // ── GET /Oferta/Editar/{id} ───────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var oferta = await _context.Ofertas
                .FirstOrDefaultAsync(o => o.Id == id &&
                    (isMaster ? o.UsuarioMasterId == usuarioId : o.UsuarioId == usuarioId));

            if (oferta == null)
            {
                TempData["Erro"] = "Oferta não encontrada.";
                return RedirectToAction("Index");
            }

            var vm = MapearParaViewModel(oferta);
            PreencherViewBagIdentidade();
            return View("Criar", vm);
        }

        // ── POST /Oferta/Salvar ───────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salvar(OfertaViewModel model)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                PreencherViewBagIdentidade();
                return View("Criar", model);
            }

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            if (model.Id.HasValue && model.Id != Guid.Empty)
            {
                // ── Atualização ──────────────────────────────────────────────────
                var oferta = await _context.Ofertas
                    .FirstOrDefaultAsync(o => o.Id == model.Id.Value &&
                        (isMaster ? o.UsuarioMasterId == usuarioId : o.UsuarioId == usuarioId));

                if (oferta == null)
                {
                    TempData["Erro"] = "Oferta não encontrada.";
                    return RedirectToAction("Index");
                }

                MapearDeViewModel(model, oferta);
                oferta.DataModificacao = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Sucesso"] = "Oferta atualizada com sucesso!";
            }
            else
            {
                // ── Criação ──────────────────────────────────────────────────────
                var oferta = new Oferta
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioId,
                    UsuarioMasterId = ObterMasterIdParaSalvar(),
                    DataCriacao = DateTime.Now
                };

                MapearDeViewModel(model, oferta);
                _context.Ofertas.Add(oferta);
                await _context.SaveChangesAsync();

                TempData["Sucesso"] = "Oferta criada com sucesso!";
            }

            return RedirectToAction("Index");
        }

        // ── POST /Oferta/Excluir ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var oferta = await _context.Ofertas
                .FirstOrDefaultAsync(o => o.Id == id &&
                    (isMaster ? o.UsuarioMasterId == usuarioId : o.UsuarioId == usuarioId));

            if (oferta == null)
            {
                TempData["Erro"] = "Oferta não encontrada.";
                return RedirectToAction("Index");
            }

            // Remove arquivos físicos associados (se existirem)
            DeletarArquivoFisico(oferta.ImagemPrincipalPath);
            if (oferta.LogoPath != null && !oferta.LogoPath.StartsWith("/uploads/user-"))
                DeletarArquivoFisico(oferta.LogoPath);

            _context.Ofertas.Remove(oferta);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Oferta excluída com sucesso.";
            return RedirectToAction("Index");
        }

        // ── POST /Oferta/Duplicar/{id} ───────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var original = await _context.Ofertas
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id &&
                    (isMaster ? o.UsuarioMasterId == usuarioId : o.UsuarioId == usuarioId));

            if (original == null)
            {
                TempData["Erro"] = "Oferta não encontrada.";
                return RedirectToAction("Index");
            }

            var copia = new Oferta
            {
                Id                  = Guid.NewGuid(),
                UsuarioId           = usuarioId,
                UsuarioMasterId     = ObterMasterIdParaSalvar(),
                DataCriacao         = DateTime.Now,
                DataModificacao     = null,

                // Metadados
                Nome                = $"Cópia de {original.Nome}",
                TemplateId          = original.TemplateId,

                // Visual
                Cor1                = original.Cor1,
                Cor2                = original.Cor2,
                Cor3                = original.Cor3,
                ImagemPrincipalPath = original.ImagemPrincipalPath,
                LogoPath            = original.LogoPath,
                SlotsJson           = original.SlotsJson,

                // Bloco principal
                TituloPrincipal     = original.TituloPrincipal,
                Subtitulo           = original.Subtitulo,
                DescricaoCurta      = original.DescricaoCurta,
                TextoComplementar   = original.TextoComplementar,
                Rodape              = original.Rodape,
                Cta                 = original.Cta,

                // Bloco comercial
                Preco               = original.Preco,
                PrecoAnterior       = original.PrecoAnterior,
                TextoAPartirDe      = original.TextoAPartirDe,
                CondicaoEspecial    = original.CondicaoEspecial,
                Parcelamento        = original.Parcelamento,
                TextoUrgencia       = original.TextoUrgencia,
                ValidadeOferta      = original.ValidadeOferta,

                // Bloco do produto
                Destino             = original.Destino,
                Origem              = original.Origem,
                PeriodoViagem       = original.PeriodoViagem,
                QtdNoites           = original.QtdNoites,
                CompanhiaAerea      = original.CompanhiaAerea,
                Hotel               = original.Hotel,
                RegimeHospedagem    = original.RegimeHospedagem,
                InclusoesOferta     = original.InclusoesOferta,
                ObservacoesCurtas   = original.ObservacoesCurtas,
                RegrasCondicoes     = original.RegrasCondicoes,

                // Bloco de contato
                WhatsApp            = original.WhatsApp,
                Telefone            = original.Telefone,
                Email               = original.Email,
                Instagram           = original.Instagram,
                Site                = original.Site,

                // Bloco institucional
                NomeAgencia         = original.NomeAgencia,
                SeloPromocional     = original.SeloPromocional,
                TagPromocional      = original.TagPromocional,
                TextoInstitucional  = original.TextoInstitucional,
            };

            _context.Ofertas.Add(copia);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Oferta duplicada com sucesso. Edite a cópia abaixo.";
            return RedirectToAction("Editar", new { id = copia.Id });
        }

        // ── POST /Oferta/UploadImagem (AJAX) ─────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> UploadImagem(IFormFile arquivo)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { erro = "Arquivo inválido." });

            if (arquivo.Length > 15 * 1024 * 1024)
                return BadRequest(new { erro = "Arquivo muito grande. Máximo 15 MB." });

            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var permitidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!permitidas.Contains(ext))
                return BadRequest(new { erro = "Formato não permitido. Use JPG, PNG ou WebP." });

            var blobUrl = await _blob.SalvarAsync(arquivo, "ofertas");
            return Ok(new { path = blobUrl });
        }

        // ── POST /Oferta/UploadLogo (AJAX) ────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> UploadLogo(IFormFile arquivo)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { erro = "Arquivo inválido." });

            if (arquivo.Length > 5 * 1024 * 1024)
                return BadRequest(new { erro = "Logo muito grande. Máximo 5 MB." });

            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var permitidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            if (!permitidas.Contains(ext))
                return BadRequest(new { erro = "Formato não permitido. Use PNG, SVG ou WebP." });

            var blobUrl = await _blob.SalvarAsync(arquivo, "ofertas-logos");
            return Ok(new { path = blobUrl });
        }

        // ── Helpers privados ──────────────────────────────────────────────────────

        private void PreencherViewBagIdentidade()
        {
            ViewBag.CorPrimaria  = HttpContext.Session.GetString("CorPrimaria")  ?? "#1a1a2e";
            ViewBag.CorSecundaria = HttpContext.Session.GetString("CorSecundaria") ?? "#e94560";
            ViewBag.CorDestaque  = HttpContext.Session.GetString("CorDestaque")  ?? "#0f3460";
            ViewBag.NomeAgencia  = HttpContext.Session.GetString("UsuarioNome")  ?? "";
            ViewBag.LogoPath     = HttpContext.Session.GetString("FotoPath")     ?? "";
        }

        private static OfertaViewModel MapearParaViewModel(Oferta o) => new()
        {
            Id                  = o.Id,
            Nome                = o.Nome,
            TemplateId          = o.TemplateId,
            Cor1                = o.Cor1,
            Cor2                = o.Cor2,
            Cor3                = o.Cor3,
            ImagemPrincipalPath = o.ImagemPrincipalPath,
            LogoPath            = o.LogoPath,
            SlotsJson           = o.SlotsJson,
            TituloPrincipal     = o.TituloPrincipal,
            Subtitulo           = o.Subtitulo,
            DescricaoCurta      = o.DescricaoCurta,
            TextoComplementar   = o.TextoComplementar,
            Rodape              = o.Rodape,
            Cta                 = o.Cta,
            Preco               = o.Preco,
            PrecoAnterior       = o.PrecoAnterior,
            TextoAPartirDe      = o.TextoAPartirDe,
            CondicaoEspecial    = o.CondicaoEspecial,
            Parcelamento        = o.Parcelamento,
            TextoUrgencia       = o.TextoUrgencia,
            ValidadeOferta      = o.ValidadeOferta,
            Destino             = o.Destino,
            Origem              = o.Origem,
            PeriodoViagem       = o.PeriodoViagem,
            QtdNoites           = o.QtdNoites,
            CompanhiaAerea      = o.CompanhiaAerea,
            Hotel               = o.Hotel,
            RegimeHospedagem    = o.RegimeHospedagem,
            InclusoesOferta     = o.InclusoesOferta,
            ObservacoesCurtas   = o.ObservacoesCurtas,
            RegrasCondicoes     = o.RegrasCondicoes,
            WhatsApp            = o.WhatsApp,
            Telefone            = o.Telefone,
            Email               = o.Email,
            Instagram           = o.Instagram,
            Site                = o.Site,
            NomeAgencia         = o.NomeAgencia,
            SeloPromocional     = o.SeloPromocional,
            TagPromocional      = o.TagPromocional,
            TextoInstitucional  = o.TextoInstitucional,
        };

        private static void MapearDeViewModel(OfertaViewModel vm, Oferta o)
        {
            o.Nome                = vm.Nome.Trim();
            o.TemplateId          = vm.TemplateId ?? "destino";
            o.Cor1                = vm.Cor1;
            o.Cor2                = vm.Cor2;
            o.Cor3                = vm.Cor3;
            o.ImagemPrincipalPath = string.IsNullOrWhiteSpace(vm.ImagemPrincipalPath) ? null : vm.ImagemPrincipalPath.Trim();
            o.LogoPath            = string.IsNullOrWhiteSpace(vm.LogoPath) ? null : vm.LogoPath.Trim();
            o.SlotsJson           = string.IsNullOrWhiteSpace(vm.SlotsJson) ? null : vm.SlotsJson.Trim();
            o.TituloPrincipal     = vm.TituloPrincipal?.Trim();
            o.Subtitulo           = vm.Subtitulo?.Trim();
            o.DescricaoCurta      = vm.DescricaoCurta?.Trim();
            o.TextoComplementar   = vm.TextoComplementar?.Trim();
            o.Rodape              = vm.Rodape?.Trim();
            o.Cta                 = vm.Cta?.Trim();
            o.Preco               = vm.Preco?.Trim();
            o.PrecoAnterior       = vm.PrecoAnterior?.Trim();
            o.TextoAPartirDe      = vm.TextoAPartirDe?.Trim();
            o.CondicaoEspecial    = vm.CondicaoEspecial?.Trim();
            o.Parcelamento        = vm.Parcelamento?.Trim();
            o.TextoUrgencia       = vm.TextoUrgencia?.Trim();
            o.ValidadeOferta      = vm.ValidadeOferta?.Trim();
            o.Destino             = vm.Destino?.Trim();
            o.Origem              = vm.Origem?.Trim();
            o.PeriodoViagem       = vm.PeriodoViagem?.Trim();
            o.QtdNoites           = vm.QtdNoites?.Trim();
            o.CompanhiaAerea      = vm.CompanhiaAerea?.Trim();
            o.Hotel               = vm.Hotel?.Trim();
            o.RegimeHospedagem    = vm.RegimeHospedagem?.Trim();
            o.InclusoesOferta     = vm.InclusoesOferta?.Trim();
            o.ObservacoesCurtas   = vm.ObservacoesCurtas?.Trim();
            o.RegrasCondicoes     = vm.RegrasCondicoes?.Trim();
            o.WhatsApp            = vm.WhatsApp?.Trim();
            o.Telefone            = vm.Telefone?.Trim();
            o.Email               = vm.Email?.Trim();
            o.Instagram           = vm.Instagram?.Trim();
            o.Site                = vm.Site?.Trim();
            o.NomeAgencia         = vm.NomeAgencia?.Trim();
            o.SeloPromocional     = vm.SeloPromocional?.Trim();
            o.TagPromocional      = vm.TagPromocional?.Trim();
            o.TextoInstitucional  = vm.TextoInstitucional?.Trim();
        }

        private void DeletarArquivoFisico(string? url)
            => _ = _blob.DeletarAsync(url);
    }
}
