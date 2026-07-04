using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class SeguroController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeguroController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private IActionResult RedirectToEditar(Guid propostaId)
        {
            TempData["ActiveTab"] = "seguro";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        // Verifica que o seguro pertence ao usuário logado e retorna a propostaId
        private async Task<(Seguro? seguro, Guid propostaId)> ObterSeguroVerificado(Guid seguroId, Guid usuarioId, bool isMaster)
        {
            var seguro = await _context.Seguros
                .Include(s => s.Proposta)
                .FirstOrDefaultAsync(s => s.Id == seguroId && (isMaster ? s.Proposta.UsuarioMasterId == usuarioId : s.Proposta.UsuarioResponsavelId == usuarioId));
            return (seguro, seguro?.PropostaId ?? Guid.Empty);
        }

        // ─── CRUD DE SEGUROS ──────────────────────────────────────────────────────

        // POST: Seguro/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid propostaId,
            string titulo,
            string? descricao,
            decimal? valor)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Erro"] = "Título do seguro é obrigatório.";
                return RedirectToEditar(propostaId);
            }

            var maxOrdem = await _context.Seguros
                .Where(s => s.PropostaId == propostaId)
                .MaxAsync(s => (int?)s.Ordem) ?? 0;

            var seguro = new Seguro
            {
                Id = Guid.NewGuid(),
                PropostaId = propostaId,
                Titulo = titulo.Trim(),
                Descricao = descricao?.Trim(),
                Valor = valor,
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.Seguros.Add(seguro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Seguro \"{seguro.Titulo}\" adicionado!";
            return RedirectToEditar(propostaId);
        }

        // POST: Seguro/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            Guid id,
            string titulo,
            string? descricao,
            decimal? valor)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var (seguro, propostaId) = await ObterSeguroVerificado(id, usuarioId, isMaster);

            if (seguro == null)
            {
                TempData["Erro"] = "Seguro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Erro"] = "Título do seguro é obrigatório.";
                return RedirectToEditar(propostaId);
            }

            seguro.Titulo = titulo.Trim();
            seguro.Descricao = descricao?.Trim();
            seguro.Valor = valor;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Seguro atualizado!";
            return RedirectToEditar(propostaId);
        }

        // POST: Seguro/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var seguro = await _context.Seguros
                .Include(s => s.Proposta)
                .Include(s => s.Imagens)
                .Include(s => s.Documentos)
                .FirstOrDefaultAsync(s => s.Id == id && (isMaster ? s.Proposta.UsuarioMasterId == usuarioId : s.Proposta.UsuarioResponsavelId == usuarioId));

            if (seguro == null)
            {
                TempData["Erro"] = "Seguro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = seguro.PropostaId;

            foreach (var img in seguro.Imagens)
                DeletarArquivoFisico(img.CaminhoImagem);
            foreach (var doc in seguro.Documentos)
                DeletarArquivoFisico(doc.CaminhoArquivo);

            _context.Seguros.Remove(seguro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Seguro excluído.";
            return RedirectToEditar(propostaId);
        }

        // ─── IMAGENS ─────────────────────────────────────────────────────────────

        // POST: Seguro/AdicionarImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarImagem(Guid seguroId, IFormFile imagem, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var (seguro, propostaId) = await ObterSeguroVerificado(seguroId, usuarioId, isMaster);

            if (seguro == null)
            {
                TempData["Erro"] = "Seguro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (imagem == null || imagem.Length == 0)
            {
                TempData["Erro"] = "Selecione uma imagem.";
                return RedirectToEditar(propostaId);
            }

            try
            {
                var caminho = await SalvarImagemAsync(imagem);

                var maxOrdem = await _context.SeguroImagens
                    .Where(i => i.SeguroId == seguroId)
                    .MaxAsync(i => (int?)i.Ordem) ?? 0;

                _context.SeguroImagens.Add(new SeguroImagem
                {
                    Id = Guid.NewGuid(),
                    SeguroId = seguroId,
                    CaminhoImagem = caminho,
                    Descricao = descricao?.Trim(),
                    Ordem = maxOrdem + 1,
                    DataCriacao = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Imagem adicionada!";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Erro"] = ex.Message;
            }

            return RedirectToEditar(propostaId);
        }

        // POST: Seguro/ExcluirImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirImagem(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var imagem = await _context.SeguroImagens
                .Include(i => i.Seguro)
                    .ThenInclude(s => s.Proposta)
                .FirstOrDefaultAsync(i => i.Id == id && (isMaster ? i.Seguro.Proposta.UsuarioMasterId == usuarioId : i.Seguro.Proposta.UsuarioResponsavelId == usuarioId));

            if (imagem == null)
            {
                TempData["Erro"] = "Imagem não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = imagem.Seguro.PropostaId;
            DeletarArquivoFisico(imagem.CaminhoImagem);
            _context.SeguroImagens.Remove(imagem);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Imagem excluída.";
            return RedirectToEditar(propostaId);
        }

        // ─── DOCUMENTOS ──────────────────────────────────────────────────────────

        // POST: Seguro/AdicionarDocumento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarDocumento(Guid seguroId, IFormFile documento)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var (seguro, propostaId) = await ObterSeguroVerificado(seguroId, usuarioId, isMaster);

            if (seguro == null)
            {
                TempData["Erro"] = "Seguro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (documento == null || documento.Length == 0)
            {
                TempData["Erro"] = "Selecione um documento.";
                return RedirectToEditar(propostaId);
            }

            if (documento.Length > 20 * 1024 * 1024)
            {
                TempData["Erro"] = "Arquivo muito grande. Máximo 20MB.";
                return RedirectToEditar(propostaId);
            }

            var ext = Path.GetExtension(documento.FileName).ToLowerInvariant();
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "seguro-documentos");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var nome = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(dir, nome);
            using (var s = new FileStream(fullPath, FileMode.Create))
                await documento.CopyToAsync(s);

            _context.SeguroDocumentos.Add(new SeguroDocumento
            {
                Id = Guid.NewGuid(),
                SeguroId = seguroId,
                NomeOriginal = documento.FileName,
                CaminhoArquivo = $"/uploads/seguro-documentos/{nome}",
                TipoArquivo = documento.ContentType,
                Tamanho = documento.Length,
                DataCriacao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Documento \"{documento.FileName}\" adicionado!";
            return RedirectToEditar(propostaId);
        }

        // POST: Seguro/ExcluirDocumento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirDocumento(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var doc = await _context.SeguroDocumentos
                .Include(d => d.Seguro)
                    .ThenInclude(s => s.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Seguro.Proposta.UsuarioMasterId == usuarioId : d.Seguro.Proposta.UsuarioResponsavelId == usuarioId));

            if (doc == null)
            {
                TempData["Erro"] = "Documento não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = doc.Seguro.PropostaId;
            DeletarArquivoFisico(doc.CaminhoArquivo);
            _context.SeguroDocumentos.Remove(doc);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Documento excluído.";
            return RedirectToEditar(propostaId);
        }

        // GET: Seguro/BaixarDocumento/{id}
        [HttpGet]
        public async Task<IActionResult> BaixarDocumento(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var doc = await _context.SeguroDocumentos
                .Include(d => d.Seguro)
                    .ThenInclude(s => s.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Seguro.Proposta.UsuarioMasterId == usuarioId : d.Seguro.Proposta.UsuarioResponsavelId == usuarioId));

            if (doc == null) return NotFound();

            var caminho = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                doc.CaminhoArquivo.TrimStart('/'));

            if (!System.IO.File.Exists(caminho)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(caminho);
            return File(bytes,
                string.IsNullOrEmpty(doc.TipoArquivo) ? "application/octet-stream" : doc.TipoArquivo,
                doc.NomeOriginal);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────────

        private async Task<string> SalvarImagemAsync(IFormFile imagem)
        {
            var ext = Path.GetExtension(imagem.FileName).ToLowerInvariant();
            var permitidos = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!permitidos.Contains(ext))
                throw new InvalidOperationException("Apenas imagens são permitidas (JPG, PNG, GIF, WebP).");
            if (imagem.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Imagem muito grande. Máximo 10MB.");

            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "seguro-imagens");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var nome = $"{Guid.NewGuid()}{ext}";
            var full = Path.Combine(dir, nome);
            using var stream = new FileStream(full, FileMode.Create);
            await imagem.CopyToAsync(stream);
            return $"/uploads/seguro-imagens/{nome}";
        }

        private static void DeletarArquivoFisico(string? caminhoRelativo)
        {
            if (string.IsNullOrEmpty(caminhoRelativo)) return;
            try
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    caminhoRelativo.TrimStart('/'));
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* swallow */ }
        }
    }
}
