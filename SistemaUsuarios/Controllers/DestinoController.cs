using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class DestinoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinoController(ApplicationDbContext context)
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

        // GET: Destino/Gerenciar/PropostaId
        public async Task<IActionResult> Gerenciar(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            // Buscar proposta com destinos e fotos
            var proposta = await _context.Propostas
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada";
                return RedirectToAction("Index", "Proposta");
            }

            return View(proposta);
        }

        // POST: Destino/AdicionarDestino
        [HttpPost]
        public async Task<IActionResult> AdicionarDestino(Guid propostaId, string nome, string? pais, string? cidade, DateTime? dataChegada, DateTime? dataSaida, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            // Verificar proposta
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            if (string.IsNullOrEmpty(nome))
            {
                TempData["Erro"] = "Nome do destino é obrigatório";
                return RedirectToAction("Gerenciar", new { propostaId });
            }

            // Validação de datas
            if (dataChegada.HasValue && dataSaida.HasValue && dataChegada > dataSaida)
            {
                TempData["Erro"] = "Data de saída deve ser posterior à data de chegada";
                return RedirectToAction("Gerenciar", new { propostaId });
            }

            // Determinar próxima ordem
            var maxOrdem = await _context.Destinos
                .Where(d => d.PropostaId == propostaId)
                .MaxAsync(d => (int?)d.Ordem) ?? 0;

            var destino = new Destino
            {
                Id = Guid.NewGuid(),
                PropostaId = propostaId,
                Nome = nome.Trim(),
                Pais = pais?.Trim(),
                Cidade = cidade?.Trim(),
                DataChegada = dataChegada,
                DataSaida = dataSaida,
                Descricao = descricao?.Trim(),
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.Destinos.Add(destino);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nome}' adicionado com sucesso!";
            return RedirectToAction("Gerenciar", new { propostaId });
        }

        // POST: Destino/EditarDestino
        [HttpPost]
        public async Task<IActionResult> EditarDestino(Guid id, string nome, string? pais, string? cidade, DateTime? dataChegada, DateTime? dataSaida, string? descricao)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .FirstOrDefaultAsync(d => d.Id == id && d.Proposta.UsuarioId == usuarioId);

            if (destino == null)
                return NotFound();

            if (string.IsNullOrEmpty(nome))
            {
                TempData["Erro"] = "Nome do destino é obrigatório";
                return RedirectToAction("Gerenciar", new { propostaId = destino.PropostaId });
            }

            // Validação de datas
            if (dataChegada.HasValue && dataSaida.HasValue && dataChegada > dataSaida)
            {
                TempData["Erro"] = "Data de saída deve ser posterior à data de chegada";
                return RedirectToAction("Gerenciar", new { propostaId = destino.PropostaId });
            }

            destino.Nome = nome.Trim();
            destino.Pais = pais?.Trim();
            destino.Cidade = cidade?.Trim();
            destino.DataChegada = dataChegada;
            destino.DataSaida = dataSaida;
            destino.Descricao = descricao?.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nome}' editado com sucesso!";
            return RedirectToAction("Gerenciar", new { propostaId = destino.PropostaId });
        }

        // POST: Destino/ExcluirDestino
        [HttpPost]
        public async Task<IActionResult> ExcluirDestino(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var destino = await _context.Destinos
                .Include(d => d.Proposta)
                .Include(d => d.Fotos)
                .FirstOrDefaultAsync(d => d.Id == id && d.Proposta.UsuarioId == usuarioId);

            if (destino == null)
                return NotFound();

            var propostaId = destino.PropostaId;
            var nomeDestino = destino.Nome;

            // Remover arquivos físicos das fotos
            foreach (var foto in destino.Fotos)
            {
                if (!string.IsNullOrEmpty(foto.CaminhoFoto))
                {
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.CaminhoFoto.TrimStart('/'));
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        System.IO.File.Delete(caminhoCompleto);
                    }
                }
            }

            _context.Destinos.Remove(destino);
            await _context.SaveChangesAsync();

            // Reordenar destinos restantes
            var destinos = await _context.Destinos
                .Where(d => d.PropostaId == propostaId)
                .OrderBy(d => d.Ordem)
                .ToListAsync();

            for (int i = 0; i < destinos.Count; i++)
            {
                destinos[i].Ordem = i + 1;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Destino '{nomeDestino}' excluído com sucesso!";
            return RedirectToAction("Gerenciar", new { propostaId });
        }

        // POST: Destino/ReordenarDestinos
        [HttpPost]
        public async Task<IActionResult> ReordenarDestinos([FromBody] ReordenarDestinosRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            // Verificar se a proposta pertence ao usuário
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == request.PropostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            // Atualizar ordem dos destinos
            for (int i = 0; i < request.DestinosIds.Count; i++)
            {
                var destino = await _context.Destinos
                    .FirstOrDefaultAsync(d => d.Id == request.DestinosIds[i] && d.PropostaId == request.PropostaId);

                if (destino != null)
                {
                    destino.Ordem = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }

    // DTO para reordenação
    public class ReordenarDestinosRequest
    {
        public Guid PropostaId { get; set; }
        public List<Guid> DestinosIds { get; set; } = new();
    }
}