using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class AiCopilotController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AiCopilotService _copilot;

        public AiCopilotController(ApplicationDbContext context, AiCopilotService copilot)
        {
            _context = context;
            _copilot = copilot;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";


        /// <summary>
        /// POST /AiCopilot/Chat
        /// Main endpoint: receives a user message + conversation history,
        /// builds proposta context from DB (edit) or form data (create),
        /// calls the AI service, and returns a structured CopilotResponse.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] CopilotChatRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { error = "Não autenticado." });

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Mensagem vazia." });

            var usuarioId = ObterUsuarioId();
            var isMaster = SessaoIsMaster();
            PropostaContext ctx;

            if (request.PropostaId.HasValue)
            {
                // Editar page — load real proposta data from DB
                var proposta = await _context.Propostas
                    .Include(p => p.PassageirosProposta)
                    .Include(p => p.Voos)
                    .Include(p => p.Destinos)
                        .ThenInclude(d => d.Hospedagens)
                    .Include(p => p.Destinos)
                        .ThenInclude(d => d.Experiencias)
                    .Include(p => p.Destinos)
                        .ThenInclude(d => d.Transportes)
                    .Include(p => p.Seguros)
                    .Include(p => p.Cliente)
                    .FirstOrDefaultAsync(p =>
                        p.Id == request.PropostaId.Value &&
                        (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

                if (proposta == null)
                    return NotFound(new { error = "Proposta não encontrada." });

                ctx = PropostaContext.FromProposta(proposta, request.AbaAtual);
            }
            else
            {
                // Criar page — use form context sent by the client
                ctx = request.FormContext ?? new PropostaContext
                {
                    IsNovaProposta = true,
                    AbaAtual = "dados",
                    NumeroPassageiros = 1
                };
                ctx.AbaAtual = request.AbaAtual;
            }

            var response = await _copilot.ChatAsync(request, ctx);
            return Json(response);
        }
    }
}
