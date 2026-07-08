using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.Dto;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class ImportacaoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ImportacaoIAService _ia;
        private readonly ImportacaoPersistenciaService _persistencia;

        public ImportacaoController(
            ApplicationDbContext context,
            ImportacaoIAService ia,
            ImportacaoPersistenciaService persistencia)
        {
            _context = context;
            _ia = ia;
            _persistencia = persistencia;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        // POST: /Importacao/CriarRascunho
        // Creates a blank draft proposta and returns its ID (used by "Criar com IA" flow in Index)
        [HttpPost]
        public async Task<IActionResult> CriarRascunho()
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            var usuarioId = ObterUsuarioId();
            var isMaster  = SessaoIsMaster();

            Guid masterUsuarioId;
            Guid responsavelId;

            if (isMaster)
            {
                masterUsuarioId = usuarioId;
                responsavelId   = usuarioId;
            }
            else
            {
                var masterIdStr = HttpContext.Session.GetString("UsuarioMasterId");
                if (string.IsNullOrEmpty(masterIdStr))
                    return Unauthorized(new { erro = "Sessão inválida." });
                masterUsuarioId = Guid.Parse(masterIdStr);
                responsavelId   = usuarioId;
            }

            var proposta = new Proposta
            {
                Titulo               = "Nova Proposta",
                StatusProposta       = StatusProposta.Rascunho,
                UsuarioMasterId      = masterUsuarioId,
                UsuarioResponsavelId = responsavelId,
                DataCriacao          = DateTime.UtcNow,
            };

            _context.Propostas.Add(proposta);
            await _context.SaveChangesAsync();

            return Json(new { propostaId = proposta.Id });
        }

        // GET: /Importacao/Iniciar/{propostaId}
        public async Task<IActionResult> Iniciar(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioId();
            var isMaster = SessaoIsMaster();

            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            ViewBag.Proposta = proposta;
            return View();
        }

        // POST: /Importacao/AnalisarDocumentos
        [HttpPost]
        public async Task<IActionResult> AnalisarDocumentos(Guid propostaId, List<IFormFile> arquivos)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (arquivos == null || !arquivos.Any())
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            var (preview, erro) = await _ia.AnalisarAsync(arquivos);

            if (erro != null)
                return StatusCode(500, new { erro });

            return Json(preview);
        }

        // POST: /Importacao/Confirmar
        [HttpPost]
        public async Task<IActionResult> Confirmar([FromBody] ConfirmarImportacaoRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (request?.Preview == null)
                return BadRequest(new { erro = "Dados inválidos." });

            var usuarioId = ObterUsuarioId();
            var isMaster = SessaoIsMaster();

            var resultado = await _persistencia.ImportarAsync(
                request.PropostaId, usuarioId, isMaster, request.Preview);

            if (!resultado.Ok)
                return StatusCode(500, new { erro = resultado.Erro });

            return Json(new
            {
                ok = true,
                propostaId = request.PropostaId,
                resumo = new
                {
                    resultado.Passageiros,
                    resultado.Voos,
                    resultado.Destinos,
                    resultado.Hospedagens,
                    resultado.Experiencias,
                    resultado.Transportes,
                    resultado.Seguros
                }
            });
        }
    }
}
