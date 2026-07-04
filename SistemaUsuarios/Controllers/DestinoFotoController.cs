using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class DestinoFotoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinoFotoController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado()
        {
            return HttpContext.Session.GetString("UsuarioId") != null;
        }

        private Guid ObterUsuarioLogadoId()
        {
            var usuarioIdString = HttpContext.Session.GetString("UsuarioId");
            return Guid.Parse(usuarioIdString);
        }

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        private IActionResult RedirectToEditar(Guid propostaId)
        {
            TempData["ActiveTab"] = "destinos";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        private async Task<string> SalvarFotoAsync(IFormFile foto)
        {
            if (foto == null || foto.Length == 0)
                return null;

            // Validar extensão
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                throw new InvalidOperationException("Apenas arquivos de imagem são permitidos (JPG, PNG, GIF, BMP, WebP)");

            // Validar tamanho (10MB)
            if (foto.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Arquivo muito grande. Máximo 10MB permitido");

            // Validar tipo MIME
            var tiposPermitidos = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!tiposPermitidos.Contains(foto.ContentType.ToLowerInvariant()))
                throw new InvalidOperationException("Tipo de arquivo não permitido");

            // Criar diretório se não existir
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "destinos");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Gerar nome único
            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoCompleto = Path.Combine(uploadsPath, nomeArquivo);

            // Salvar arquivo
            using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
            {
                await foto.CopyToAsync(stream);
            }

            return $"/uploads/destinos/{nomeArquivo}";
        }

        // POST: DestinoFoto/AdicionarFoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarFoto(Guid destinoId, IFormFile[] fotos, string? descricao, bool principal = false)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            // Verificar se o destino pertence ao usuário
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .Include(d => d.Fotos)
                .FirstOrDefaultAsync(d => d.Id == destinoId && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

            if (destino == null)
            {
                TempData["Erro"] = "Destino não encontrado";
                return RedirectToAction("Index", "Proposta");
            }

            var fotosValidas = (fotos ?? Array.Empty<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (!fotosValidas.Any())
            {
                TempData["Erro"] = "Selecione ao menos uma foto para fazer upload";
                return RedirectToEditar(destino.PropostaId);
            }

            try
            {
                // Se alguma foto será principal, limpar flag das existentes
                if (principal)
                {
                    var fotosExistentes = await _context.DestinoFotos
                        .Where(f => f.DestinoId == destinoId)
                        .ToListAsync();

                    foreach (var fotoExistente in fotosExistentes)
                        fotoExistente.Principal = false;
                }

                var maxOrdem = await _context.DestinoFotos
                    .Where(f => f.DestinoId == destinoId)
                    .MaxAsync(f => (int?)f.Ordem) ?? 0;

                var erros = new List<string>();
                int adicionadas = 0;

                for (int i = 0; i < fotosValidas.Count; i++)
                {
                    try
                    {
                        var caminhoFoto = await SalvarFotoAsync(fotosValidas[i]);

                        // Apenas a primeira foto recebe o flag principal (quando marcado)
                        var isPrincipal = principal && i == 0;

                        _context.DestinoFotos.Add(new DestinoFoto
                        {
                            Id = Guid.NewGuid(),
                            DestinoId = destinoId,
                            CaminhoFoto = caminhoFoto,
                            Descricao = descricao?.Trim(),
                            Principal = isPrincipal,
                            Ordem = maxOrdem + i + 1,
                            DataCriacao = DateTime.Now
                        });
                        adicionadas++;
                    }
                    catch (InvalidOperationException ex)
                    {
                        erros.Add($"{fotosValidas[i].FileName}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                if (erros.Any())
                    TempData["Erro"] = string.Join(" | ", erros);
                else
                    TempData["Sucesso"] = adicionadas == 1
                        ? "Foto adicionada com sucesso!"
                        : $"{adicionadas} fotos adicionadas com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro ao salvar as fotos. Tente novamente.";
                Console.WriteLine($"Erro ao salvar foto: {ex.Message}");
            }

            return RedirectToEditar(destino.PropostaId);
        }

        // POST: DestinoFoto/ExcluirFoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirFoto(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var foto = await _context.DestinoFotos
                .Include(f => f.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id && (isMaster ? f.Destino.Proposta.UsuarioMasterId == usuarioId : f.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (foto == null)
            {
                TempData["Erro"] = "Foto não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = foto.Destino.PropostaId;
            var destinoId = foto.DestinoId;

            // Remover arquivo físico
            if (!string.IsNullOrEmpty(foto.CaminhoFoto))
            {
                try
                {
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.CaminhoFoto.TrimStart('/'));
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        System.IO.File.Delete(caminhoCompleto);
                    }
                }
                catch (Exception ex)
                {
                    // Log do erro mas continue com a exclusão do banco
                    Console.WriteLine($"Erro ao excluir arquivo físico: {ex.Message}");
                }
            }

            _context.DestinoFotos.Remove(foto);
            await _context.SaveChangesAsync();

            // Reordenar fotos restantes
            var fotosRestantes = await _context.DestinoFotos
                .Where(f => f.DestinoId == destinoId)
                .OrderBy(f => f.Ordem)
                .ToListAsync();

            for (int i = 0; i < fotosRestantes.Count; i++)
            {
                fotosRestantes[i].Ordem = i + 1;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Foto excluída com sucesso!";
            return RedirectToEditar(propostaId);
        }

        // POST: DestinoFoto/DefinirPrincipal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirPrincipal(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var foto = await _context.DestinoFotos
                .Include(f => f.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id && (isMaster ? f.Destino.Proposta.UsuarioMasterId == usuarioId : f.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (foto == null)
            {
                TempData["Erro"] = "Foto não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            // Remover flag principal de todas as fotos do destino
            var todasFotosDestino = await _context.DestinoFotos
                .Where(f => f.DestinoId == foto.DestinoId)
                .ToListAsync();

            foreach (var fotoDestino in todasFotosDestino)
            {
                fotoDestino.Principal = false;
            }

            // Definir esta foto como principal
            foto.Principal = true;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Foto definida como principal!";
            return RedirectToEditar(foto.Destino.PropostaId);
        }

        // POST: DestinoFoto/ReordenarFotos
        [HttpPost]
        public async Task<IActionResult> ReordenarFotos([FromBody] ReordenarFotosRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == request.DestinoId &&
                    (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

            if (destino == null)
                return NotFound();

            if (request.FotosIds == null || !request.FotosIds.Any())
                return BadRequest("Lista de IDs de fotos não pode estar vazia");

            // Rejeitar IDs duplicados
            if (request.FotosIds.Distinct().Count() != request.FotosIds.Count)
                return BadRequest("Lista contém IDs duplicados.");

            // Carregar todas as fotos do destino de uma vez
            var fotos = await _context.DestinoFotos
                .Where(f => f.DestinoId == request.DestinoId)
                .ToListAsync();

            // Validar que todos os IDs enviados pertencem a este destino
            var fotosIds = fotos.Select(f => f.Id).ToHashSet();
            if (request.FotosIds.Any(id => !fotosIds.Contains(id)))
                return BadRequest("Um ou mais IDs não pertencem a este destino.");

            // Duas fases para evitar violação do índice único {DestinoId, Ordem}
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Fase 1: ordens temporárias negativas (sem conflito)
                for (int i = 0; i < fotos.Count; i++)
                    fotos[i].Ordem = -1000 - i;

                await _context.SaveChangesAsync();

                // Fase 2: ordem final conforme a lista enviada
                for (int i = 0; i < request.FotosIds.Count; i++)
                {
                    var foto = fotos.First(f => f.Id == request.FotosIds[i]);
                    foto.Ordem = i + 1;
                }

                // Fotos não incluídas na lista recebem ordens após as listadas
                var naoListadas = fotos.Where(f => !request.FotosIds.Contains(f.Id)).ToList();
                for (int i = 0; i < naoListadas.Count; i++)
                    naoListadas[i].Ordem = request.FotosIds.Count + i + 1;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Fotos reordenadas com sucesso!" });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Erro ao reordenar fotos." });
            }
        }

        // POST: DestinoFoto/EditarFoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarFoto(Guid id, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var foto = await _context.DestinoFotos
                .Include(f => f.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id && (isMaster ? f.Destino.Proposta.UsuarioMasterId == usuarioId : f.Destino.Proposta.UsuarioResponsavelId == usuarioId));

            if (foto == null)
            {
                TempData["Erro"] = "Foto não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            foto.Descricao = descricao?.Trim();
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Descrição da foto atualizada!";
            return RedirectToEditar(foto.Destino.PropostaId);
        }

        // GET: DestinoFoto/ObterFoto/{id} - Para exibir foto diretamente
        [HttpGet]
        public async Task<IActionResult> ObterFoto(Guid id)
        {
            var foto = await _context.DestinoFotos
                .Include(f => f.Destino)
                    .ThenInclude(d => d.Proposta)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (foto == null)
                return NotFound();

            // Se a proposta não tem link público ativo, verificar se é o dono
            if (!foto.Destino.Proposta.LinkPublicoAtivo)
            {
                if (!UsuarioLogado())
                    return Unauthorized();

                var usuarioId = ObterUsuarioLogadoId();
                var isMaster = SessaoIsMaster();
                var autorizado = isMaster
                    ? foto.Destino.Proposta.UsuarioMasterId == usuarioId
                    : foto.Destino.Proposta.UsuarioResponsavelId == usuarioId;
                if (!autorizado)
                    return Forbid();
            }

            if (string.IsNullOrEmpty(foto.CaminhoFoto))
                return NotFound();

            var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.CaminhoFoto.TrimStart('/'));

            if (!System.IO.File.Exists(caminhoCompleto))
                return NotFound();

            var contentType = GetContentType(foto.CaminhoFoto);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(caminhoCompleto);

            return File(fileBytes, contentType);
        }

        // Método auxiliar para obter content type baseado na extensão
        private string GetContentType(string caminho)
        {
            var extensao = Path.GetExtension(caminho).ToLowerInvariant();
            return extensao switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        // GET: DestinoFoto/ListarFotos/{destinoId} - API para listar fotos de um destino
        [HttpGet]
        public async Task<IActionResult> ListarFotos(Guid destinoId)
        {
            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .Include(d => d.Fotos.OrderBy(f => f.Ordem))
                .FirstOrDefaultAsync(d => d.Id == destinoId);

            if (destino == null)
                return NotFound();

            // Se a proposta não tem link público ativo, verificar se é o dono
            if (!destino.Proposta.LinkPublicoAtivo)
            {
                if (!UsuarioLogado())
                    return Unauthorized();

                var usuarioId = ObterUsuarioLogadoId();
                var isMaster = SessaoIsMaster();
                var autorizado = isMaster
                    ? destino.Proposta.UsuarioMasterId == usuarioId
                    : destino.Proposta.UsuarioResponsavelId == usuarioId;
                if (!autorizado)
                    return Forbid();
            }

            var fotos = destino.Fotos.Select(f => new
            {
                id = f.Id,
                caminhoFoto = f.CaminhoFoto,
                descricao = f.Descricao,
                principal = f.Principal,
                ordem = f.Ordem,
                dataCriacao = f.DataCriacao
            }).ToList();

            return Json(fotos);
        }

        // POST: DestinoFoto/DefinirMultiplasPrincipais - Para definir múltiplas fotos principais em lote
        [HttpPost]
        public async Task<IActionResult> DefinirMultiplasPrincipais([FromBody] DefinirPrincipaisRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            foreach (var item in request.DestinosEFotos)
            {
                // Verificar se o destino pertence ao usuário
                var destino = await _context.Destinos
                    .Include(d => d.Proposta)
                    .FirstOrDefaultAsync(d => d.Id == item.DestinoId && (isMaster ? d.Proposta.UsuarioMasterId == usuarioId : d.Proposta.UsuarioResponsavelId == usuarioId));

                if (destino == null) continue;

                // Remover flag principal de todas as fotos do destino
                var todasFotos = await _context.DestinoFotos
                    .Where(f => f.DestinoId == item.DestinoId)
                    .ToListAsync();

                foreach (var foto in todasFotos)
                {
                    foto.Principal = false;
                }

                // Definir a foto especificada como principal
                var fotoPrincipal = todasFotos.FirstOrDefault(f => f.Id == item.FotoId);
                if (fotoPrincipal != null)
                {
                    fotoPrincipal.Principal = true;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Fotos principais atualizadas!" });
        }
    }

    // DTOs para as requisições
    public class ReordenarFotosRequest
    {
        public Guid DestinoId { get; set; }
        public List<Guid> FotosIds { get; set; } = new();
    }

    public class DefinirPrincipaisRequest
    {
        public List<DestinoPrincipalItem> DestinosEFotos { get; set; } = new();
    }

    public class DestinoPrincipalItem
    {
        public Guid DestinoId { get; set; }
        public Guid FotoId { get; set; }
    }
}