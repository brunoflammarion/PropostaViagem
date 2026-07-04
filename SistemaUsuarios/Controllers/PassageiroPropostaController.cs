using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Controllers
{
    public class PassageiroPropostaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PassageiroPropostaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioLogadoId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";


        private IActionResult RedirectToPassageiros(Guid propostaId)
        {
            TempData["ActiveTab"] = "passageiros";
            return RedirectToAction("Editar", "Proposta", new { id = propostaId });
        }

        // POST: PassageiroProposta/Adicionar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adicionar(
            Guid propostaId,
            string nome,
            DateTime? dataNascimento,
            Genero? genero,
            RelacionamentoPassageiro? relacionamento,
            string? observacoes)
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

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do passageiro é obrigatório.";
                return RedirectToPassageiros(propostaId);
            }

            var maxOrdem = await _context.PassageirosProposta
                .Where(p => p.PropostaId == propostaId)
                .MaxAsync(p => (int?)p.Ordem) ?? 0;

            var passageiro = new PassageiroProposta
            {
                Id = Guid.NewGuid(),
                PropostaId = propostaId,
                Nome = nome.Trim(),
                DataNascimento = dataNascimento,
                Genero = genero,
                Relacionamento = relacionamento,
                Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim(),
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.PassageirosProposta.Add(passageiro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Passageiro \"{passageiro.Nome}\" adicionado!";
            return RedirectToPassageiros(propostaId);
        }

        // POST: PassageiroProposta/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            Guid id,
            string nome,
            DateTime? dataNascimento,
            Genero? genero,
            RelacionamentoPassageiro? relacionamento,
            string? observacoes)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var passageiro = await _context.PassageirosProposta
                .Include(p => p.Proposta)
                .FirstOrDefaultAsync(p => p.Id == id && (isMaster ? p.Proposta.UsuarioMasterId == usuarioId : p.Proposta.UsuarioResponsavelId == usuarioId));

            if (passageiro == null)
            {
                TempData["Erro"] = "Passageiro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do passageiro é obrigatório.";
                return RedirectToPassageiros(passageiro.PropostaId);
            }

            passageiro.Nome = nome.Trim();
            passageiro.DataNascimento = dataNascimento;
            passageiro.Genero = genero;
            passageiro.Relacionamento = relacionamento;
            passageiro.Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Passageiro atualizado!";
            return RedirectToPassageiros(passageiro.PropostaId);
        }

        // POST: PassageiroProposta/Excluir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var passageiro = await _context.PassageirosProposta
                .Include(p => p.Proposta)
                .FirstOrDefaultAsync(p => p.Id == id && (isMaster ? p.Proposta.UsuarioMasterId == usuarioId : p.Proposta.UsuarioResponsavelId == usuarioId));

            if (passageiro == null)
            {
                TempData["Erro"] = "Passageiro não encontrado.";
                return RedirectToAction("Index", "Proposta");
            }

            var propostaId = passageiro.PropostaId;
            _context.PassageirosProposta.Remove(passageiro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Passageiro removido.";
            return RedirectToPassageiros(propostaId);
        }
    }
}
