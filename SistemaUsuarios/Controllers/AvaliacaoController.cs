using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class AvaliacaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AvaliacaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// POST /Avaliacao/Salvar
        /// Called from the public proposal page. Accepts a JSON payload with all ratings.
        /// Upserts each item: if the client has already rated an item (same PropostaId + TipoItem + ItemId),
        /// the rating is updated rather than duplicated.
        /// </summary>
        [HttpPost]
        [Route("Avaliacao/Salvar")]
        public async Task<IActionResult> Salvar([FromBody] AvaliacaoRequest request)
        {
            if (request?.PropostaId == Guid.Empty || request?.Itens == null || !request.Itens.Any())
                return BadRequest(new { ok = false, erro = "Dados inválidos." });

            // Confirm proposal exists and link is active
            var proposta = await _context.Propostas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PropostaId && p.LinkPublicoAtivo);

            if (proposta == null)
                return NotFound(new { ok = false, erro = "Proposta não encontrada." });

            foreach (var item in request.Itens)
            {
                if (item.Nota < 1 || item.Nota > 5) continue;

                // Upsert by PropostaId + TipoItem + ItemId
                var existing = await _context.AvaliacoesCliente
                    .FirstOrDefaultAsync(a =>
                        a.PropostaId == request.PropostaId &&
                        a.TipoItem   == item.TipoItem &&
                        a.ItemId     == item.ItemId);

                if (existing != null)
                {
                    existing.Nota        = item.Nota;
                    existing.Comentario  = item.Comentario;
                    existing.Favorito    = item.Favorito;
                    existing.DataCriacao = DateTime.Now;
                }
                else
                {
                    _context.AvaliacoesCliente.Add(new AvaliacaoCliente
                    {
                        Id          = Guid.NewGuid(),
                        PropostaId  = request.PropostaId,
                        TipoItem    = item.TipoItem,
                        ItemId      = item.ItemId,
                        Nota        = item.Nota,
                        Comentario  = item.Comentario,
                        Favorito    = item.Favorito,
                        DataCriacao = DateTime.Now,
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────────

    public class AvaliacaoRequest
    {
        public Guid PropostaId { get; set; }
        public List<AvaliacaoItemDto> Itens { get; set; } = new();
    }

    public class AvaliacaoItemDto
    {
        public TipoItemAvaliacao TipoItem { get; set; }
        public Guid ItemId { get; set; }
        public int Nota { get; set; }
        public string? Comentario { get; set; }
        public bool Favorito { get; set; }
    }
}
