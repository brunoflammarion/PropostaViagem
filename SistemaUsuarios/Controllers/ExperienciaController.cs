using SistemaUsuarios.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class ExperienciaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blob;

        public ExperienciaController(ApplicationDbContext context, BlobStorageService blob)
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

        // Resolve propostaId a partir de um destinoId, verificando autoria
        private async Task<(Destino? destino, Guid propostaId)> ObterDestinoVerificado(Guid destinoId, Guid usuarioId, bool isMaster)
        {
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == destinoId && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));
            return (destino, destino?.PropostaId ?? Guid.Empty);
        }

        // ─── CRUD DE EXPERIÊNCIAS ─────────────────────────────────────────────────

        // POST: Experiencia/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid destinoId,
            string tipoPasseio,
            string? descricao,
            string? videoUrl,
            decimal? valor,
            DateTime? dataInicio,
            DateTime? dataFim,
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

            if (string.IsNullOrWhiteSpace(tipoPasseio))
            {
                TempData["Erro"] = "Tipo de passeio é obrigatório.";
                return RedirectToEditar(propostaId);
            }

            if (dataInicio.HasValue && dataFim.HasValue && dataFim < dataInicio)
            {
                TempData["Erro"] = "Data/hora de fim não pode ser anterior ao início da experiência.";
                return RedirectToEditar(propostaId);
            }

            var maxOrdem = await _context.Experiencias
                .Where(e => e.DestinoId == destinoId)
                .MaxAsync(e => (int?)e.Ordem) ?? 0;

            var experiencia = new Experiencia
            {
                Id = Guid.NewGuid(),
                DestinoId = destinoId,
                TipoPasseio = tipoPasseio.Trim(),
                Descricao = descricao?.Trim(),
                VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim(),
                Valor = valor,
                DataInicio = dataInicio,
                DataFim = dataFim,
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.Experiencias.Add(experiencia);
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
                        _context.ExperienciaImagens.Add(new ExperienciaImagem
                        {
                            Id            = Guid.NewGuid(),
                            ExperienciaId = experiencia.Id,
                            CaminhoImagem = caminho,
                            Ordem         = i + 1,
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
                    ? $"Experiência criada, mas {errosFoto.Count} imagem(ns) não puderam ser salvas: {string.Join("; ", errosFoto)}"
                    : $"Experiência \"{experiencia.TipoPasseio}\" adicionada com {imagensValidas.Count} imagem(ns)!";
            }
            else
            {
                TempData["Sucesso"] = $"Experiência \"{experiencia.TipoPasseio}\" adicionada!";
            }

            return RedirectToEditar(propostaId);
        }

        // POST: Experiencia/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            Guid id,
            string tipoPasseio,
            string? descricao,
            string? videoUrl,
            decimal? valor,
            DateTime? dataInicio,
            DateTime? dataFim)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var experiencia = await _context.Experiencias
                .Include(e => e.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(e => e.Id == id && (isMaster ? e.Destino.Proposta.UsuarioMasterId == usuarioId : e.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (experiencia == null)
            {
                TempData["Erro"] = "Experiência não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(tipoPasseio))
            {
                TempData["Erro"] = "Tipo de passeio é obrigatório.";
                return RedirectToEditar(experiencia.Destino.PropostaId);
            }

            if (dataInicio.HasValue && dataFim.HasValue && dataFim < dataInicio)
            {
                TempData["Erro"] = "Data/hora de fim não pode ser anterior ao início da experiência.";
                return RedirectToEditar(experiencia.Destino.PropostaId);
            }

            experiencia.TipoPasseio = tipoPasseio.Trim();
            experiencia.Descricao = descricao?.Trim();
            experiencia.VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
            experiencia.Valor = valor;
            experiencia.DataInicio = dataInicio;
            experiencia.DataFim = dataFim;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Experiência atualizada!";
            return RedirectToEditar(experiencia.Destino.PropostaId);
        }

        // POST: Experiencia/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var experiencia = await _context.Experiencias
                .Include(e => e.Destino)
                    .ThenInclude(d => d.Proposta)
                .Include(e => e.Imagens)
                .Include(e => e.Arquivos)
                .FirstOrDefaultAsync(e => e.Id == id && (isMaster ? e.Destino.Proposta.UsuarioMasterId == usuarioId : e.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (experiencia == null)
            {
                TempData["Erro"] = "Experiência não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = experiencia.Destino.PropostaId;

            // Remover arquivos físicos
            foreach (var img in experiencia.Imagens)
                DeletarArquivoFisico(img.CaminhoImagem);
            foreach (var arq in experiencia.Arquivos)
                DeletarArquivoFisico(arq.CaminhoArquivo);

            _context.Experiencias.Remove(experiencia);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Experiência excluída.";
            return RedirectToEditar(propostaId);
        }

        // ─── IMAGENS ─────────────────────────────────────────────────────────────

        // POST: Experiencia/AdicionarImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarImagem(Guid experienciaId, IFormFile[] imagens, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var experiencia = await _context.Experiencias
                .Include(e => e.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(e => e.Id == experienciaId && (isMaster ? e.Destino.Proposta.UsuarioMasterId == usuarioId : e.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (experiencia == null)
            {
                TempData["Erro"] = "Experiência não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var imagensValidas = (imagens ?? Array.Empty<IFormFile>()).Where(i => i != null && i.Length > 0).ToList();
            if (!imagensValidas.Any())
            {
                TempData["Erro"] = "Selecione ao menos uma imagem.";
                return RedirectToEditar(experiencia.Destino.PropostaId);
            }

            try
            {
                var maxOrdem = await _context.ExperienciaImagens
                    .Where(i => i.ExperienciaId == experienciaId)
                    .MaxAsync(i => (int?)i.Ordem) ?? 0;

                var erros = new List<string>();
                int adicionadas = 0;

                for (int i = 0; i < imagensValidas.Count; i++)
                {
                    try
                    {
                        var caminho = await SalvarImagemAsync(imagensValidas[i]);
                        _context.ExperienciaImagens.Add(new ExperienciaImagem
                        {
                            Id = Guid.NewGuid(),
                            ExperienciaId = experienciaId,
                            CaminhoImagem = caminho,
                            Descricao = descricao?.Trim(),
                            Ordem = maxOrdem + i + 1,
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

            return RedirectToEditar(experiencia.Destino.PropostaId);
        }

        // POST: Experiencia/ExcluirImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirImagem(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var imagem = await _context.ExperienciaImagens
                .Include(i => i.Experiencia)
                    .ThenInclude(e => e.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(i => i.Id == id && (isMaster ? i.Experiencia.Destino.Proposta.UsuarioMasterId == usuarioId : i.Experiencia.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (imagem == null)
            {
                TempData["Erro"] = "Imagem não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = imagem.Experiencia.Destino.PropostaId;
            var experienciaId = imagem.ExperienciaId;
            DeletarArquivoFisico(imagem.CaminhoImagem);
            _context.ExperienciaImagens.Remove(imagem);
            await _context.SaveChangesAsync();

            var restantes = await _context.ExperienciaImagens
                .Where(i => i.ExperienciaId == experienciaId)
                .OrderBy(i => i.Ordem)
                .ToListAsync();
            for (int i = 0; i < restantes.Count; i++)
                restantes[i].Ordem = i + 1;
            if (restantes.Any()) await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Imagem excluída.";
            return RedirectToEditar(propostaId);
        }

        // POST: Experiencia/ReordenarImagens
        [HttpPost]
        public async Task<IActionResult> ReordenarImagens([FromBody] ReordenarImagensExpRequest request)
        {
            if (!UsuarioLogado()) return Unauthorized();
            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var experiencia = await _context.Experiencias
                .Include(e => e.Destino).ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(e => e.Id == request.ExperienciaId &&
                    (isMaster ? e.Destino.Proposta.UsuarioMasterId == usuarioId : e.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (experiencia == null) return NotFound();

            for (int i = 0; i < request.ImagensIds.Count; i++)
            {
                var img = await _context.ExperienciaImagens
                    .FirstOrDefaultAsync(f => f.Id == request.ImagensIds[i] && f.ExperienciaId == request.ExperienciaId);
                if (img != null)
                    img.Ordem = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── ARQUIVOS / VOUCHERS ─────────────────────────────────────────────────

        // POST: Experiencia/AdicionarArquivo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarArquivo(Guid experienciaId, IFormFile arquivo)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var experiencia = await _context.Experiencias
                .Include(e => e.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(e => e.Id == experienciaId && (isMaster ? e.Destino.Proposta.UsuarioMasterId == usuarioId : e.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (experiencia == null)
            {
                TempData["Erro"] = "Experiência não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (arquivo == null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Selecione um arquivo.";
                return RedirectToEditar(experiencia.Destino.PropostaId);
            }

            if (arquivo.Length > 20 * 1024 * 1024)
            {
                TempData["Erro"] = "Arquivo muito grande. Máximo 20MB.";
                return RedirectToEditar(experiencia.Destino.PropostaId);
            }

            var blobUrl = await _blob.SalvarArquivoAsync(arquivo, "experiencia-arquivos");

            _context.ExperienciaArquivos.Add(new ExperienciaArquivo
            {
                Id = Guid.NewGuid(),
                ExperienciaId = experienciaId,
                NomeOriginal = arquivo.FileName,
                CaminhoArquivo = blobUrl,
                TipoArquivo = arquivo.ContentType,
                Tamanho = arquivo.Length,
                DataCriacao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Arquivo \"{arquivo.FileName}\" adicionado!";
            return RedirectToEditar(experiencia.Destino.PropostaId);
        }

        // POST: Experiencia/ExcluirArquivo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirArquivo(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var arquivo = await _context.ExperienciaArquivos
                .Include(a => a.Experiencia)
                    .ThenInclude(e => e.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Experiencia.Destino.Proposta.UsuarioMasterId == usuarioId : a.Experiencia.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (arquivo == null)
            {
                TempData["Erro"] = "Arquivo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = arquivo.Experiencia.Destino.PropostaId;
            DeletarArquivoFisico(arquivo.CaminhoArquivo);
            _context.ExperienciaArquivos.Remove(arquivo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Arquivo excluído.";
            return RedirectToEditar(propostaId);
        }

        // GET: Experiencia/BaixarArquivo/{id}
        [HttpGet]
        public async Task<IActionResult> BaixarArquivo(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var arquivo = await _context.ExperienciaArquivos
                .Include(a => a.Experiencia)
                    .ThenInclude(e => e.Destino)
                        .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Experiencia.Destino.Proposta.UsuarioMasterId == usuarioId : a.Experiencia.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (arquivo == null) return NotFound();

            // Blob URL: redireciona diretamente
            if (arquivo.CaminhoArquivo.StartsWith("http"))
                return Redirect(arquivo.CaminhoArquivo);

            // Caminho local legado (fallback)
            var caminho = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                arquivo.CaminhoArquivo.TrimStart('/'));
            if (!System.IO.File.Exists(caminho)) return NotFound();
            var bytes = await System.IO.File.ReadAllBytesAsync(caminho);
            return File(bytes,
                string.IsNullOrEmpty(arquivo.TipoArquivo) ? "application/octet-stream" : arquivo.TipoArquivo,
                arquivo.NomeOriginal);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────────

        private Task<string> SalvarImagemAsync(IFormFile imagem)
            => _blob.SalvarAsync(imagem, "experiencia-imagens");

        private void DeletarArquivoFisico(string? url)
            => _ = _blob.DeletarAsync(url);
    }

    public class ReordenarImagensExpRequest
    {
        public Guid ExperienciaId { get; set; }
        public List<Guid> ImagensIds { get; set; } = new();
    }
}
