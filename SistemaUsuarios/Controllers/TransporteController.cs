using SistemaUsuarios.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class TransporteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blob;

        public TransporteController(ApplicationDbContext context, BlobStorageService blob)
        {
            _blob = blob;
            _context = context;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";


        private IActionResult RedirectToEditar(Guid propostaId)
        {
            TempData["ActiveTab"] = "destinos";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        private async Task<(Destino? destino, Guid propostaId)> ObterDestinoVerificado(Guid destinoId, Guid usuarioId, bool isMaster)
        {
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == destinoId && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));
            return (destino, destino?.PropostaId ?? Guid.Empty);
        }

        // ─── CRUD DE TRANSPORTES ──────────────────────────────────────────────────

        // POST: Transporte/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid destinoId,
            string titulo,
            string? descricao,
            decimal? valor,
            IFormFile[]? imagens = null)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var (destino, propostaId) = await ObterDestinoVerificado(destinoId, usuarioId, isMaster);

            if (destino == null)
            {
                TempData["Erro"] = "Destino não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Erro"] = "Título é obrigatório.";
                return RedirectToEditar(propostaId);
            }

            var maxOrdem = await _context.Transportes
                .Where(t => t.DestinoId == destinoId)
                .MaxAsync(t => (int?)t.Ordem) ?? 0;

            var transporte = new Transporte
            {
                Id = Guid.NewGuid(),
                DestinoId = destinoId,
                Titulo = titulo.Trim(),
                Descricao = descricao?.Trim(),
                Valor = valor,
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.Transportes.Add(transporte);
            await _context.SaveChangesAsync();

            var imagensValidas = (imagens ?? Array.Empty<IFormFile>())
                .Where(f => f != null && f.Length > 0).ToList();

            if (imagensValidas.Any())
            {
                var errosFoto = new List<string>();
                for (int i = 0; i < imagensValidas.Count; i++)
                {
                    try
                    {
                        var caminho = await SalvarImagemAsync(imagensValidas[i]);
                        _context.TransporteImagens.Add(new TransporteImagem
                        {
                            Id            = Guid.NewGuid(),
                            TransporteId  = transporte.Id,
                            CaminhoImagem = caminho,
                            Ordem         = i + 1,
                            Principal     = i == 0,
                            DataCriacao   = DateTime.Now
                        });
                    }
                    catch (InvalidOperationException ex)
                    {
                        errosFoto.Add($"{imagensValidas[i].FileName}: {ex.Message}");
                    }
                }
                await _context.SaveChangesAsync();

                TempData[errosFoto.Any() ? "Aviso" : "Sucesso"] = errosFoto.Any()
                    ? $"Transporte criado, mas {errosFoto.Count} imagem(ns) não puderam ser salvas: {string.Join("; ", errosFoto)}"
                    : $"Transporte \"{transporte.Titulo}\" adicionado com {imagensValidas.Count} imagem(ns)!";
            }
            else
            {
                TempData["Sucesso"] = $"Transporte \"{transporte.Titulo}\" adicionado!";
            }

            return RedirectToEditar(propostaId);
        }

        // POST: Transporte/Editar
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
            var transporte = await _context.Transportes
                .Include(t => t.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(t => t.Id == id && (isMaster ? t.Destino.Proposta.UsuarioMasterId == usuarioId : t.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (transporte == null)
            {
                TempData["Erro"] = "Transporte não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(titulo))
            {
                TempData["Erro"] = "Título é obrigatório.";
                return RedirectToEditar(transporte.Destino.PropostaId);
            }

            transporte.Titulo = titulo.Trim();
            transporte.Descricao = descricao?.Trim();
            transporte.Valor = valor;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Transporte atualizado!";
            return RedirectToEditar(transporte.Destino.PropostaId);
        }

        // POST: Transporte/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var transporte = await _context.Transportes
                .Include(t => t.Destino)
                    .ThenInclude(d => d.Proposta)
                .Include(t => t.Imagens)
                .Include(t => t.Documentos)
                .FirstOrDefaultAsync(t => t.Id == id && (isMaster ? t.Destino.Proposta.UsuarioMasterId == usuarioId : t.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (transporte == null)
            {
                TempData["Erro"] = "Transporte não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = transporte.Destino.PropostaId;

            foreach (var img in transporte.Imagens)
                DeletarArquivoFisico(img.CaminhoImagem);
            foreach (var doc in transporte.Documentos)
                DeletarArquivoFisico(doc.CaminhoArquivo);

            _context.Transportes.Remove(transporte);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Transporte excluído.";
            return RedirectToEditar(propostaId);
        }

        // ─── IMAGENS ─────────────────────────────────────────────────────────────

        // POST: Transporte/AdicionarImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarImagem(Guid transporteId, IFormFile[] imagens, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var transporte = await _context.Transportes
                .Include(t => t.Destino)
                    .ThenInclude(d => d.Proposta)
                .Include(t => t.Imagens)
                .FirstOrDefaultAsync(t => t.Id == transporteId && (isMaster ? t.Destino.Proposta.UsuarioMasterId == usuarioId : t.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (transporte == null)
            {
                TempData["Erro"] = "Transporte não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var imagensValidas = (imagens ?? Array.Empty<IFormFile>()).Where(i => i != null && i.Length > 0).ToList();
            if (!imagensValidas.Any())
            {
                TempData["Erro"] = "Selecione ao menos uma imagem.";
                return RedirectToEditar(transporte.Destino.PropostaId);
            }

            try
            {
                var maxOrdem = transporte.Imagens.Count > 0 ? transporte.Imagens.Max(i => i.Ordem) : 0;
                var erros = new List<string>();
                int adicionadas = 0;

                for (int i = 0; i < imagensValidas.Count; i++)
                {
                    try
                    {
                        var caminho = await SalvarImagemAsync(imagensValidas[i]);

                        // Primeira imagem do transporte é marcada como principal
                        var isPrincipal = !transporte.Imagens.Any() && i == 0;

                        _context.TransporteImagens.Add(new TransporteImagem
                        {
                            Id = Guid.NewGuid(),
                            TransporteId = transporteId,
                            CaminhoImagem = caminho,
                            Descricao = descricao?.Trim(),
                            Ordem = maxOrdem + i + 1,
                            Principal = isPrincipal,
                            DataCriacao = DateTime.Now
                        });
                        adicionadas++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        erros.Add($"{imagensValidas[i].FileName}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                if (erros.Any())
                    TempData["Erro"] = string.Join(" | ", erros);
                else
                    TempData["Sucesso"] = adicionadas == 1 ? "Imagem adicionada!" : $"{adicionadas} imagens adicionadas!";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Erro"] = ex.Message;
            }

            return RedirectToEditar(transporte.Destino.PropostaId);
        }

        // POST: Transporte/ExcluirImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirImagem(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var imagem = await _context.TransporteImagens
                .Include(i => i.Transporte)
                    .ThenInclude(t => t.Destino)
                        .ThenInclude(d => d.Proposta)
                .Include(i => i.Transporte)
                    .ThenInclude(t => t.Imagens)
                .FirstOrDefaultAsync(i => i.Id == id && (isMaster ? i.Transporte.Destino.Proposta.UsuarioMasterId == usuarioId : i.Transporte.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (imagem == null)
            {
                TempData["Erro"] = "Imagem não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = imagem.Transporte.Destino.PropostaId;
            var eraPrincipal = imagem.Principal;
            var transporteId = imagem.TransporteId;

            DeletarArquivoFisico(imagem.CaminhoImagem);
            _context.TransporteImagens.Remove(imagem);
            await _context.SaveChangesAsync();

            // Reordenar restantes
            var restantes = await _context.TransporteImagens
                .Where(i => i.TransporteId == transporteId)
                .OrderBy(i => i.Ordem)
                .ToListAsync();
            for (int i = 0; i < restantes.Count; i++)
                restantes[i].Ordem = i + 1;

            // Se era a principal, promover a próxima
            if (eraPrincipal && restantes.Any())
                restantes[0].Principal = true;

            if (restantes.Any()) await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Imagem excluída.";
            return RedirectToEditar(propostaId);
        }

        // POST: Transporte/ReordenarImagens
        [HttpPost]
        public async Task<IActionResult> ReordenarImagens([FromBody] ReordenarImagensTranspRequest request)
        {
            if (!UsuarioLogado()) return Unauthorized();
            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var transporte = await _context.Transportes
                .Include(t => t.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(t => t.Id == request.TransporteId &&
                    (isMaster ? t.Destino.Proposta.UsuarioMasterId == usuarioId : t.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (transporte == null) return NotFound();

            for (int i = 0; i < request.ImagensIds.Count; i++)
            {
                var img = await _context.TransporteImagens
                    .FirstOrDefaultAsync(f => f.Id == request.ImagensIds[i] && f.TransporteId == request.TransporteId);
                if (img != null)
                    img.Ordem = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── DOCUMENTOS ──────────────────────────────────────────────────────────

        // POST: Transporte/AdicionarDocumento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarDocumento(Guid transporteId, IFormFile documento)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var transporte = await _context.Transportes
                .Include(t => t.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(t => t.Id == transporteId && (isMaster ? t.Destino.Proposta.UsuarioMasterId == usuarioId : t.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (transporte == null)
            {
                TempData["Erro"] = "Transporte não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (documento == null || documento.Length == 0)
            {
                TempData["Erro"] = "Selecione um arquivo.";
                return RedirectToEditar(transporte.Destino.PropostaId);
            }

            if (documento.Length > 20 * 1024 * 1024)
            {
                TempData["Erro"] = "Arquivo muito grande. Máximo 20MB.";
                return RedirectToEditar(transporte.Destino.PropostaId);
            }

            var blobUrl = await _blob.SalvarArquivoAsync(documento, "transporte-documentos");

            _context.TransporteDocumentos.Add(new TransporteDocumento
            {
                Id = Guid.NewGuid(),
                TransporteId = transporteId,
                NomeOriginal = documento.FileName,
                CaminhoArquivo = blobUrl,
                TipoArquivo = documento.ContentType,
                Tamanho = documento.Length,
                DataCriacao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Documento \"{documento.FileName}\" adicionado!";
            return RedirectToEditar(transporte.Destino.PropostaId);
        }

        // POST: Transporte/ExcluirDocumento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirDocumento(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var documento = await _context.TransporteDocumentos
                .Include(d => d.Transporte)
                    .ThenInclude(t => t.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Transporte.Destino.Proposta.UsuarioMasterId == usuarioId : d.Transporte.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (documento == null)
            {
                TempData["Erro"] = "Documento não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = documento.Transporte.Destino.PropostaId;
            DeletarArquivoFisico(documento.CaminhoArquivo);
            _context.TransporteDocumentos.Remove(documento);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Documento excluído.";
            return RedirectToEditar(propostaId);
        }

        // GET: Transporte/BaixarDocumento/{id}
        [HttpGet]
        public async Task<IActionResult> BaixarDocumento(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var documento = await _context.TransporteDocumentos
                .Include(d => d.Transporte)
                    .ThenInclude(t => t.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && (isMaster ? d.Transporte.Destino.Proposta.UsuarioMasterId == usuarioId : d.Transporte.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (documento == null) return NotFound();

            if (documento.CaminhoArquivo.StartsWith("http"))
                return Redirect(documento.CaminhoArquivo);

            var caminho = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                documento.CaminhoArquivo.TrimStart('/'));
            if (!System.IO.File.Exists(caminho)) return NotFound();
            var bytes = await System.IO.File.ReadAllBytesAsync(caminho);
            return File(bytes,
                string.IsNullOrEmpty(documento.TipoArquivo) ? "application/octet-stream" : documento.TipoArquivo,
                documento.NomeOriginal);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────────

        private Task<string> SalvarImagemAsync(IFormFile imagem)
            => _blob.SalvarAsync(imagem, "transporte-imagens");

        private void DeletarArquivoFisico(string? url)
            => _ = _blob.DeletarAsync(url);
    }

    public class ReordenarImagensTranspRequest
    {
        public Guid TransporteId { get; set; }
        public List<Guid> ImagensIds { get; set; } = new();
    }
}
