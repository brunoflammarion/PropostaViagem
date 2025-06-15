using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using BCrypt.Net;

namespace SistemaUsuarios.Controllers
{
    public class LandingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LandingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Landing
        public IActionResult Index()
        {
            // Estatísticas reais para a landing page
            var estatisticas = new LandingStatisticsViewModel
            {
                TotalAgentes = _context.Usuarios.Count(u => u.Status == StatusUsuario.Ativo),
                TotalPropostas = _context.Propostas.Count(),
                TotalVisualizacoes = _context.PropostaVisualizacoes.Count(),
                MediaVisualizacoesPorProposta = _context.Propostas.Any() ?
                    _context.PropostaVisualizacoes.Count() / (double)_context.Propostas.Count() : 0
            };

            return View(estatisticas);
        }

        // POST: Landing/Cadastro
        [HttpPost]
        public async Task<IActionResult> Cadastro(LandingCadastroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Retornar JSON para AJAX
                return Json(new
                {
                    success = false,
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            try
            {
                // Verificar se email já existe
                if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower()))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Este email já está cadastrado. Faça login ou use outro email."
                    });
                }

                // Limpar formatação do telefone
                string telefoneLimpo = LimparTelefone(model.Telefone);

                // Criar usuário
                var usuario = new Usuario
                {
                    Nome = model.Nome.Trim(),
                    Email = model.Email.ToLower().Trim(),
                    Telefone = telefoneLimpo,
                    CPF = "00000000000", // CPF temporário - será solicitado depois
                    Senha = BCrypt.Net.BCrypt.HashPassword("123456"), // Senha temporária
                    DataCriacao = DateTime.Now,
                    Status = StatusUsuario.Novo
                };

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                // Log da conversão para analytics
                await LogConversaoLanding(model);

                return Json(new
                {
                    success = true,
                    message = "Cadastro realizado com sucesso! Redirecionando...",
                    redirectUrl = Url.Action("CompletarCadastro", new { id = usuario.Id })
                });
            }
            catch (Exception ex)
            {
                // Log do erro
                Console.WriteLine($"Erro no cadastro landing: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "Erro interno. Tente novamente em alguns minutos."
                });
            }
        }

        // GET: Landing/CompletarCadastro/{id}
        public async Task<IActionResult> CompletarCadastro(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return RedirectToAction("Index");
            }

            var model = new CompletarCadastroViewModel
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Telefone = FormatarTelefone(usuario.Telefone)
            };

            return View(model);
        }

        // POST: Landing/CompletarCadastro
        [HttpPost]
        public async Task<IActionResult> CompletarCadastro(CompletarCadastroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = await _context.Usuarios.FindAsync(model.Id);
            if (usuario == null)
            {
                return RedirectToAction("Index");
            }

            try
            {
                // Limpar formatação
                string cpfLimpo = LimparCPF(model.CPF);
                string telefoneLimpo = LimparTelefone(model.Telefone);

                // Verificar se CPF já existe
                if (await _context.Usuarios.AnyAsync(u => u.CPF == cpfLimpo && u.Id != model.Id))
                {
                    ModelState.AddModelError("CPF", "Este CPF já está em uso");
                    return View(model);
                }

                // Atualizar usuário
                usuario.Nome = model.Nome.Trim();
                usuario.Telefone = telefoneLimpo;
                usuario.CPF = cpfLimpo;
                usuario.Senha = BCrypt.Net.BCrypt.HashPassword(model.Senha);
                usuario.Status = StatusUsuario.Ativo;

                await _context.SaveChangesAsync();

                // Criar sessão
                HttpContext.Session.SetString("UsuarioId", usuario.Id.ToString());
                HttpContext.Session.SetString("UsuarioNome", usuario.Nome);

                TempData["Sucesso"] = "Bem-vindo! Sua conta foi criada com sucesso. Vamos criar sua primeira proposta!";

                return RedirectToAction("Criar", "Proposta");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao completar cadastro: {ex.Message}");
                ModelState.AddModelError("", "Erro interno. Tente novamente.");
                return View(model);
            }
        }

        // API: Landing/Estatisticas
        [HttpGet]
        public async Task<IActionResult> Estatisticas()
        {
            var stats = new
            {
                agentesAtivos = await _context.Usuarios.CountAsync(u => u.Status == StatusUsuario.Ativo),
                propostas = await _context.Propostas.CountAsync(),
                visualizacoes = await _context.PropostaVisualizacoes.CountAsync(),
                taxaConversao = await CalcularTaxaConversao(),
                faturamentoMedio = 50000, // Valor exemplo
                avaliacaoMedia = 4.9
            };

            return Json(stats);
        }

        // Métodos auxiliares
        private string LimparCPF(string cpf)
        {
            return System.Text.RegularExpressions.Regex.Replace(cpf ?? "", @"[^\d]", "");
        }

        private string LimparTelefone(string telefone)
        {
            return System.Text.RegularExpressions.Regex.Replace(telefone ?? "", @"[^\d]", "");
        }

        private string FormatarTelefone(string telefone)
        {
            if (string.IsNullOrEmpty(telefone))
                return telefone;

            if (telefone.Length == 11)
                return $"({telefone.Substring(0, 2)}) {telefone.Substring(2, 5)}-{telefone.Substring(7, 4)}";
            else if (telefone.Length == 10)
                return $"({telefone.Substring(0, 2)}) {telefone.Substring(2, 4)}-{telefone.Substring(6, 4)}";

            return telefone;
        }

        private async Task LogConversaoLanding(LandingCadastroViewModel model)
        {
            try
            {
                // Aqui você pode implementar um log específico para conversões da landing
                // Por exemplo, salvar em uma tabela de analytics ou enviar para serviço externo

                var logEntry = new
                {
                    Data = DateTime.Now,
                    Email = model.Email,
                    TipoAgente = model.TipoAgente,
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    IP = GetClientIP(),
                    Referrer = Request.Headers["Referer"].ToString()
                };

                // Log para console (em produção, usar logger apropriado)
                Console.WriteLine($"🎯 Nova conversão landing: {model.Email} - {model.TipoAgente}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao logar conversão: {ex.Message}");
            }
        }

        private async Task<double> CalcularTaxaConversao()
        {
            try
            {
                var totalVisualizacoes = await _context.PropostaVisualizacoes.CountAsync();
                var totalInteracoes = await _context.PropostaVisualizacoes
                    .CountAsync(v => v.ClicouEmail || v.ClicouWhatsApp);

                if (totalVisualizacoes == 0) return 0;
                return Math.Round((double)totalInteracoes / totalVisualizacoes * 100, 1);
            }
            catch
            {
                return 25.5; // Valor padrão
            }
        }

        private string GetClientIP()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
            else if (Request.Headers.ContainsKey("X-Real-IP"))
                ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();

            return ipAddress ?? "Unknown";
        }
    }
}