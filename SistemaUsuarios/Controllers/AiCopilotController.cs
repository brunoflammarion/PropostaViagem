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
        private readonly ImportacaoIAService _importacaoIA;

        public AiCopilotController(
            ApplicationDbContext context,
            AiCopilotService copilot,
            ImportacaoIAService importacaoIA)
        {
            _context = context;
            _copilot = copilot;
            _importacaoIA = importacaoIA;
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

        /// <summary>
        /// POST /AiCopilot/AnalisarArquivos
        /// Accepts multipart files + optional propostaId.
        /// Returns a chat-formatted import analysis with structured preview.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AnalisarArquivos([FromForm] Guid? propostaId, [FromForm] List<IFormFile> arquivos)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (arquivos == null || !arquivos.Any())
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            // Verify proposta ownership if propostaId provided
            if (propostaId.HasValue)
            {
                var usuarioId = ObterUsuarioId();
                var isMaster  = SessaoIsMaster();
                var existe = await _context.Propostas.AnyAsync(p =>
                    p.Id == propostaId.Value &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

                if (!existe)
                    return NotFound(new { erro = "Proposta não encontrada ou sem permissão." });
            }

            var (draft, erro) = await _importacaoIA.AnalisarAsync(arquivos);

            if (draft == null)
                return BadRequest(new { erro = erro ?? "Erro ao processar os documentos." });

            // Contar itens para saber se há algo a importar
            var temItens = draft.Passageiros.Any() || draft.Voos.Any() ||
                           draft.Destinos.Any() || draft.Seguros.Any();

            // Se a IA não gerou mensagemInicial, construímos uma de fallback
            if (string.IsNullOrWhiteSpace(draft.MensagemInicial))
            {
                var partes = new List<string>();
                if (draft.Passageiros.Count > 0) partes.Add($"{draft.Passageiros.Count} passageiro{(draft.Passageiros.Count != 1 ? "s" : "")}");
                if (draft.Voos.Count > 0) partes.Add($"{draft.Voos.Count} voo{(draft.Voos.Count != 1 ? "s" : "")}");
                if (draft.Destinos.Count > 0)
                {
                    partes.Add($"{draft.Destinos.Count} destino{(draft.Destinos.Count != 1 ? "s" : "")}");
                    var h = draft.Destinos.Sum(d => d.Hospedagens.Count);
                    var e = draft.Destinos.Sum(d => d.Experiencias.Count);
                    var t = draft.Destinos.Sum(d => d.Transportes.Count);
                    if (h > 0) partes.Add($"{h} hospedagem{(h != 1 ? "ns" : "")}");
                    if (e > 0) partes.Add($"{e} experiência{(e != 1 ? "s" : "")}");
                    if (t > 0) partes.Add($"{t} transporte{(t != 1 ? "s" : "")}");
                }
                if (draft.Seguros.Count > 0) partes.Add($"{draft.Seguros.Count} seguro{(draft.Seguros.Count != 1 ? "s" : "")}");

                draft.MensagemInicial = temItens
                    ? $"Analisei o documento e encontrei: {string.Join(", ", partes)}.\n\nPosso adicionar esses itens à sua proposta?"
                    : "Analisei o documento mas não encontrei itens estruturados (voos, hospedagens, passageiros, etc.). Tente com um documento diferente.";
            }

            return Json(new { tipo = "analise_documentos", draft, temItens });
        }
    }
}
