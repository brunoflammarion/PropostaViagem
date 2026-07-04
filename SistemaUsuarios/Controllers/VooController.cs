using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class VooController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFlightLookupService _flightLookup;

        public VooController(ApplicationDbContext context, IFlightLookupService flightLookup)
        {
            _context = context;
            _flightLookup = flightLookup;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        private IActionResult RedirectToEditar(Guid propostaId)
        {
            TempData["ActiveTab"] = "aereo";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        // ─── VOOS ────────────────────────────────────────────────────────────────

        // POST: Voo/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid propostaId,
            string numeroVoo,
            TipoVoo tipoVoo,
            string companhia,
            string? classe,
            string? duracao,
            string origem,
            string destino,
            DateTime? horarioSaida,
            DateTime? horarioChegada,
            string? bagagemItemPessoalDescricao,
            string? bagagemItemPessoalMedidas,
            decimal? bagagemMaoPeso,
            string? bagagemMaoMedidas,
            decimal? bagagemDespachadaPeso,
            string? bagagemDespachadaMedidas)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(numeroVoo) || string.IsNullOrWhiteSpace(companhia)
                || string.IsNullOrWhiteSpace(origem) || string.IsNullOrWhiteSpace(destino))
            {
                TempData["Erro"] = "Número do voo, companhia, origem e destino são obrigatórios.";
                return RedirectToEditar(propostaId);
            }

            var maxOrdem = await _context.Voos
                .Where(v => v.PropostaId == propostaId)
                .MaxAsync(v => (int?)v.Ordem) ?? 0;

            var voo = new Voo
            {
                Id = Guid.NewGuid(),
                PropostaId = propostaId,
                NumeroVoo = numeroVoo.Trim().ToUpperInvariant(),
                TipoVoo = tipoVoo,
                Companhia = companhia.Trim(),
                Classe = classe?.Trim(),
                Duracao = duracao?.Trim(),
                Origem = origem.Trim(),
                Destino = destino.Trim(),
                HorarioSaida = horarioSaida,
                HorarioChegada = horarioChegada,
                BagagemItemPessoalDescricao = bagagemItemPessoalDescricao?.Trim(),
                BagagemItemPessoalMedidas   = bagagemItemPessoalMedidas?.Trim(),
                BagagemMaoPeso              = bagagemMaoPeso,
                BagagemMaoMedidas           = bagagemMaoMedidas?.Trim(),
                BagagemDespachadaPeso       = bagagemDespachadaPeso,
                BagagemDespachadaMedidas    = bagagemDespachadaMedidas?.Trim(),
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.Voos.Add(voo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Voo {voo.NumeroVoo} adicionado com sucesso!";
            return RedirectToEditar(propostaId);
        }

        // POST: Voo/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            Guid id,
            string numeroVoo,
            TipoVoo tipoVoo,
            string companhia,
            string? classe,
            string? duracao,
            string origem,
            string destino,
            DateTime? horarioSaida,
            DateTime? horarioChegada,
            string? bagagemItemPessoalDescricao,
            string? bagagemItemPessoalMedidas,
            decimal? bagagemMaoPeso,
            string? bagagemMaoMedidas,
            decimal? bagagemDespachadaPeso,
            string? bagagemDespachadaMedidas)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == id && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(numeroVoo) || string.IsNullOrWhiteSpace(companhia)
                || string.IsNullOrWhiteSpace(origem) || string.IsNullOrWhiteSpace(destino))
            {
                TempData["Erro"] = "Número do voo, companhia, origem e destino são obrigatórios.";
                return RedirectToEditar(voo.PropostaId);
            }

            voo.NumeroVoo = numeroVoo.Trim().ToUpperInvariant();
            voo.TipoVoo = tipoVoo;
            voo.Companhia = companhia.Trim();
            voo.Classe = classe?.Trim();
            voo.Duracao = duracao?.Trim();
            voo.Origem = origem.Trim();
            voo.Destino = destino.Trim();
            voo.HorarioSaida = horarioSaida;
            voo.HorarioChegada = horarioChegada;
            voo.BagagemItemPessoalDescricao = bagagemItemPessoalDescricao?.Trim();
            voo.BagagemItemPessoalMedidas   = bagagemItemPessoalMedidas?.Trim();
            voo.BagagemMaoPeso              = bagagemMaoPeso;
            voo.BagagemMaoMedidas           = bagagemMaoMedidas?.Trim();
            voo.BagagemDespachadaPeso       = bagagemDespachadaPeso;
            voo.BagagemDespachadaMedidas    = bagagemDespachadaMedidas?.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Voo {voo.NumeroVoo} atualizado!";
            return RedirectToEditar(voo.PropostaId);
        }

        // POST: Voo/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == id && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = voo.PropostaId;
            _context.Voos.Remove(voo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Voo excluído com sucesso.";
            return RedirectToEditar(propostaId);
        }

        // ─── PASSAGEIROS ──────────────────────────────────────────────────────────

        // POST: Voo/AdicionarPassageiro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarPassageiro(
            Guid vooId,
            string nome,
            string? assento,
            int bagagensDespachadas = 0)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == vooId && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do passageiro é obrigatório.";
                return RedirectToEditar(voo.PropostaId);
            }

            var passageiro = new PassageiroVoo
            {
                Id = Guid.NewGuid(),
                VooId = vooId,
                Nome = nome.Trim(),
                Assento = string.IsNullOrWhiteSpace(assento) ? null : assento.Trim().ToUpperInvariant(),
                BagagensDespachadas = Math.Max(0, bagagensDespachadas),
                DataCriacao = DateTime.Now
            };

            _context.PassageirosVoo.Add(passageiro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Passageiro {passageiro.Nome} adicionado!";
            return RedirectToEditar(voo.PropostaId);
        }

        // POST: Voo/EditarPassageiro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPassageiro(
            Guid id,
            string nome,
            string? assento,
            int bagagensDespachadas = 0)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var passageiro = await _context.PassageirosVoo
                .Include(p => p.Voo)
                    .ThenInclude(v => v.Proposta)
                .FirstOrDefaultAsync(p => p.Id == id && (isMaster ? p.Voo.Proposta.UsuarioMasterId == usuarioId : p.Voo.Proposta.UsuarioResponsavelId == usuarioId));

            if (passageiro == null)
            {
                TempData["Erro"] = "Passageiro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do passageiro é obrigatório.";
                return RedirectToEditar(passageiro.Voo.PropostaId);
            }

            passageiro.Nome = nome.Trim();
            passageiro.Assento = string.IsNullOrWhiteSpace(assento) ? null : assento.Trim().ToUpperInvariant();
            passageiro.BagagensDespachadas = Math.Max(0, bagagensDespachadas);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Passageiro atualizado!";
            return RedirectToEditar(passageiro.Voo.PropostaId);
        }

        // POST: Voo/ExcluirPassageiro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirPassageiro(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var passageiro = await _context.PassageirosVoo
                .Include(p => p.Voo)
                    .ThenInclude(v => v.Proposta)
                .FirstOrDefaultAsync(p => p.Id == id && (isMaster ? p.Voo.Proposta.UsuarioMasterId == usuarioId : p.Voo.Proposta.UsuarioResponsavelId == usuarioId));

            if (passageiro == null)
            {
                TempData["Erro"] = "Passageiro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = passageiro.Voo.PropostaId;
            _context.PassageirosVoo.Remove(passageiro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Passageiro removido.";
            return RedirectToEditar(propostaId);
        }

        // ─── OBSERVAÇÃO ───────────────────────────────────────────────────────────

        // POST: Voo/SalvarObservacao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarObservacao(Guid vooId, string? observacao, IFormFile? imagem)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == vooId && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            voo.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();

            if (imagem != null && imagem.Length > 0)
            {
                try
                {
                    voo.ObservacaoImagemPath = await SalvarImagemAsync(imagem);
                }
                catch (InvalidOperationException ex)
                {
                    TempData["Erro"] = ex.Message;
                    return RedirectToEditar(voo.PropostaId);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Observação salva!";
            return RedirectToEditar(voo.PropostaId);
        }

        // POST: Voo/ExcluirObservacaoImagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirObservacaoImagem(Guid vooId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == vooId && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (!string.IsNullOrEmpty(voo.ObservacaoImagemPath))
                DeletarArquivoFisico(voo.ObservacaoImagemPath);

            voo.ObservacaoImagemPath = null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Imagem removida.";
            return RedirectToEditar(voo.PropostaId);
        }

        // ─── ANEXOS ───────────────────────────────────────────────────────────────

        // POST: Voo/AdicionarAnexo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAnexo(Guid vooId, IFormFile arquivo)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var voo = await _context.Voos
                .Include(v => v.Proposta)
                .FirstOrDefaultAsync(v => v.Id == vooId && (isMaster ? v.Proposta.UsuarioMasterId == usuarioId : v.Proposta.UsuarioResponsavelId == usuarioId));

            if (voo == null)
            {
                TempData["Erro"] = "Voo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (arquivo == null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Selecione um arquivo para upload.";
                return RedirectToEditar(voo.PropostaId);
            }

            if (arquivo.Length > 20 * 1024 * 1024)
            {
                TempData["Erro"] = "Arquivo muito grande. Máximo 20MB.";
                return RedirectToEditar(voo.PropostaId);
            }

            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "voo-anexos");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoCompleto = Path.Combine(uploadsPath, nomeArquivo);

            using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                await arquivo.CopyToAsync(stream);

            var anexo = new VooAnexo
            {
                Id = Guid.NewGuid(),
                VooId = vooId,
                NomeOriginal = arquivo.FileName,
                CaminhoArquivo = $"/uploads/voo-anexos/{nomeArquivo}",
                TipoArquivo = arquivo.ContentType,
                Tamanho = arquivo.Length,
                DataCriacao = DateTime.Now
            };

            _context.VooAnexos.Add(anexo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Anexo \"{arquivo.FileName}\" adicionado!";
            return RedirectToEditar(voo.PropostaId);
        }

        // POST: Voo/ExcluirAnexo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirAnexo(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var anexo = await _context.VooAnexos
                .Include(a => a.Voo)
                    .ThenInclude(v => v.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Voo.Proposta.UsuarioMasterId == usuarioId : a.Voo.Proposta.UsuarioResponsavelId == usuarioId));

            if (anexo == null)
            {
                TempData["Erro"] = "Anexo não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = anexo.Voo.PropostaId;
            DeletarArquivoFisico(anexo.CaminhoArquivo);
            _context.VooAnexos.Remove(anexo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Anexo excluído.";
            return RedirectToEditar(propostaId);
        }

        // GET: Voo/BaixarAnexo/{id}
        [HttpGet]
        public async Task<IActionResult> BaixarAnexo(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var anexo = await _context.VooAnexos
                .Include(a => a.Voo)
                    .ThenInclude(v => v.Proposta)
                .FirstOrDefaultAsync(a => a.Id == id && (isMaster ? a.Voo.Proposta.UsuarioMasterId == usuarioId : a.Voo.Proposta.UsuarioResponsavelId == usuarioId));

            if (anexo == null)
                return NotFound();

            var caminho = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                anexo.CaminhoArquivo.TrimStart('/'));

            if (!System.IO.File.Exists(caminho))
                return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(caminho);
            return File(bytes, string.IsNullOrEmpty(anexo.TipoArquivo)
                ? "application/octet-stream" : anexo.TipoArquivo,
                anexo.NomeOriginal);
        }

        // ─── AERODATABOX ──────────────────────────────────────────────────────────

        // GET: Voo/ConsultarVoo?codigoVoo=AFL1482  (AJAX)
        [HttpGet]
        public async Task<IActionResult> ConsultarVoo(string codigoVoo)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var r = await _flightLookup.ConsultarVooAsync(codigoVoo);

            if (!string.IsNullOrEmpty(r.Erro))
                return BadRequest(new { erro = r.Erro });

            return Ok(new
            {
                found          = true,
                // Companhia
                companhia      = r.Companhia,
                companhiaIata  = r.CompanhiaIata,
                companhiaIcao  = r.CompanhiaIcao,
                // Voo
                codigoVoo      = r.CodigoVoo,
                identIata      = r.IdentIata,
                status         = r.Status,
                codeshare      = r.Codeshare,
                modeloAeronave = r.ModeloAeronave,
                // Origem
                origem               = r.Origem,
                origemIata           = r.OrigemIata,
                origemIcao           = r.OrigemIcao,
                origemCidade         = r.OrigemCidade,
                origemPais           = r.OrigemPais,
                origemFuso           = r.OrigemFuso,
                origemTerminal       = r.OrigemTerminal,
                origemPortao         = r.OrigemPortao,
                origemCheckIn        = r.OrigemCheckIn,
                saidaLocalProgramada = r.SaidaLocalProgramada?.ToString("yyyy-MM-ddTHH:mm"),
                saidaLocalRevisada   = r.SaidaLocalRevisada?.ToString("yyyy-MM-ddTHH:mm"),
                // Destino
                destino                  = r.Destino,
                destinoIata              = r.DestinoIata,
                destinoIcao              = r.DestinoIcao,
                destinoCidade            = r.DestinoCidade,
                destinoPais              = r.DestinoPais,
                destinoFuso              = r.DestinoFuso,
                chegadaLocalProgramada   = r.ChegadaLocalProgramada?.ToString("yyyy-MM-ddTHH:mm"),
                chegadaLocalRevisada     = r.ChegadaLocalRevisada?.ToString("yyyy-MM-ddTHH:mm"),
                chegadaLocalPrevista     = r.ChegadaLocalPrevista?.ToString("yyyy-MM-ddTHH:mm"),
                // Horários principais
                horarioSaida   = r.HorarioSaida?.ToString("yyyy-MM-ddTHH:mm"),
                horarioChegada = r.HorarioChegada?.ToString("yyyy-MM-ddTHH:mm"),
                duracao        = r.Duracao,
            });
        }

        // ─── HELPERS PRIVADOS ─────────────────────────────────────────────────────

        private async Task<string> SalvarImagemAsync(IFormFile imagem)
        {
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(imagem.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(ext))
                throw new InvalidOperationException("Apenas imagens são permitidas (JPG, PNG, GIF, WebP).");

            if (imagem.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Imagem muito grande. Máximo 10MB.");

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "voo-obs");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var nome = $"{Guid.NewGuid()}{ext}";
            var caminho = Path.Combine(uploadsPath, nome);

            using var stream = new FileStream(caminho, FileMode.Create);
            await imagem.CopyToAsync(stream);

            return $"/uploads/voo-obs/{nome}";
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
            catch { /* log and swallow */ }
        }
    }
}
