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

        private async Task<Proposta?> ObterPropostaAutorizada(Guid propostaId, Guid usuarioId)
        {
            var isMaster = SessaoIsMaster();
            return await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));
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
            string? observacoes,
            FaixaEtariaPassageiro? faixaEtaria,
            bool faixaIsAproximada = false,
            ModoBebe? modoBebe = null)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var proposta = await ObterPropostaAutorizada(propostaId, usuarioId);

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
                FaixaEtaria = faixaIsAproximada ? faixaEtaria : null,
                FaixaIsAproximada = faixaIsAproximada && !dataNascimento.HasValue,
                ModoBebe = modoBebe,
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
            string? observacoes,
            FaixaEtariaPassageiro? faixaEtaria,
            bool faixaIsAproximada = false,
            ModoBebe? modoBebe = null)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var passageiro = await _context.PassageirosProposta
                .Include(p => p.Proposta)
                .FirstOrDefaultAsync(p => p.Id == id &&
                    (isMaster ? p.Proposta.UsuarioMasterId == usuarioId : p.Proposta.UsuarioResponsavelId == usuarioId));

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
            passageiro.FaixaEtaria = faixaIsAproximada ? faixaEtaria : null;
            passageiro.FaixaIsAproximada = faixaIsAproximada && !dataNascimento.HasValue;
            passageiro.ModoBebe = modoBebe;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Passageiro atualizado!";
            return RedirectToPassageiros(passageiro.PropostaId);
        }

        // POST: PassageiroProposta/AdicionarCliente
        // Cria um passageiro a partir dos dados do cliente vinculado à proposta
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarCliente(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();

            var proposta = await _context.Propostas
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (proposta.Cliente == null)
            {
                TempData["Erro"] = "Esta proposta não tem um responsável vinculado.";
                return RedirectToPassageiros(propostaId);
            }

            var jaPassageiro = await _context.PassageirosProposta
                .AnyAsync(p => p.PropostaId == propostaId && p.ClienteId == proposta.ClienteId);

            if (jaPassageiro)
            {
                TempData["Aviso"] = "O responsável já está na lista de passageiros.";
                return RedirectToPassageiros(propostaId);
            }

            var maxOrdem = await _context.PassageirosProposta
                .Where(p => p.PropostaId == propostaId)
                .MaxAsync(p => (int?)p.Ordem) ?? 0;

            var passageiro = new PassageiroProposta
            {
                Id = Guid.NewGuid(),
                PropostaId = propostaId,
                ClienteId = proposta.ClienteId,
                Nome = proposta.Cliente.Nome,
                DataNascimento = proposta.Cliente.DataNascimento,
                Genero = proposta.Cliente.Genero,
                Ordem = maxOrdem + 1,
                DataCriacao = DateTime.Now
            };

            _context.PassageirosProposta.Add(passageiro);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{proposta.Cliente.Nome} adicionado(a) como passageiro!";
            return RedirectToPassageiros(propostaId);
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
                .FirstOrDefaultAsync(p => p.Id == id &&
                    (isMaster ? p.Proposta.UsuarioMasterId == usuarioId : p.Proposta.UsuarioResponsavelId == usuarioId));

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
