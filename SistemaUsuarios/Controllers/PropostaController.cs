using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;

namespace SistemaUsuarios.Controllers
{
    public class PropostaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropostaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogado()
        {
            return HttpContext.Session.GetString("UsuarioId") != null;
        }

        private async Task<string> SalvarFotoAsync(IFormFile foto)
        {
            if (foto == null || foto.Length == 0)
                return null;

            // Validar se é imagem
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                throw new InvalidOperationException("Apenas arquivos de imagem são permitidos (JPG, PNG, GIF, BMP)");

            // Validar tamanho (5MB)
            if (foto.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Arquivo muito grande. Máximo 5MB permitido");

            // Validar tipo MIME
            var tiposPermitidos = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!tiposPermitidos.Contains(foto.ContentType.ToLowerInvariant()))
                throw new InvalidOperationException("Tipo de arquivo não permitido");

            // Criar diretório se não existir
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "propostas");
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

            // Retornar caminho relativo
            return $"/uploads/propostas/{nomeArquivo}";
        }

        private Guid ObterUsuarioLogadoId()
        {
            var usuarioIdString = HttpContext.Session.GetString("UsuarioId");
            return Guid.Parse(usuarioIdString);
        }

        public async Task<IActionResult> Index(PropostaFiltroViewModel filtro)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            // Buscar todos os usuários para o dropdown
            filtro.Usuarios = await _context.Usuarios.OrderBy(u => u.Nome).ToListAsync();

            // Query base
            var query = _context.Propostas.Include(p => p.Usuario).AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrEmpty(filtro.TermoBusca))
            {
                query = query.Where(p => p.Titulo.Contains(filtro.TermoBusca));
            }

            if (filtro.FiltroStatus.HasValue)
            {
                query = query.Where(p => p.StatusProposta == filtro.FiltroStatus.Value);
            }

            if (filtro.DataInicioFiltro.HasValue)
            {
                query = query.Where(p => p.DataInicio >= filtro.DataInicioFiltro.Value);
            }

            if (filtro.DataFimFiltro.HasValue)
            {
                query = query.Where(p => p.DataFim <= filtro.DataFimFiltro.Value);
            }

            if (filtro.FiltroUsuario.HasValue)
            {
                query = query.Where(p => p.UsuarioId == filtro.FiltroUsuario.Value);
            }

            if (filtro.FiltroLinkAtivo.HasValue)
            {
                query = query.Where(p => p.LinkPublicoAtivo == filtro.FiltroLinkAtivo.Value);
            }

            // Buscar propostas
            var propostas = await query.OrderByDescending(p => p.DataCriacao).ToListAsync();

            // Converter para ViewModel
            filtro.Propostas = propostas.Select(p => new PropostaListViewModel
            {
                Id = p.Id,
                Titulo = p.Titulo,
                DataCriacao = p.DataCriacao,
                DataInicio = p.DataInicio,
                DataFim = p.DataFim,
                NumeroPassageiros = p.NumeroPassageiros,
                NumeroCriancas = p.NumeroCriancas,
                StatusProposta = p.StatusProposta,
                NomeUsuario = p.Usuario?.Nome ?? "N/A",
                UsuarioId = p.UsuarioId,
                LinkPublicoAtivo = p.LinkPublicoAtivo,
                FotoCapa = p.FotoCapa,
                DataModificacao = p.DataModificacao
            }).ToList();

            // Calcular estatísticas
            filtro.TotalPropostas = filtro.Propostas.Count;
            filtro.TotalRascunhos = filtro.Propostas.Count(p => p.StatusProposta == StatusProposta.Rascunho);
            filtro.TotalEnviadas = filtro.Propostas.Count(p => p.StatusProposta == StatusProposta.Enviada);
            filtro.TotalAprovadas = filtro.Propostas.Count(p => p.StatusProposta == StatusProposta.Aprovada);

            return View(filtro);
        }

        [HttpGet]
        public async Task<IActionResult> Criar()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            await CarregarDadosParaView();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Criar(PropostaViewModel model)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            // DEFINIR USUÁRIO DA SESSÃO
            model.UsuarioId = ObterUsuarioLogadoId();

            // REMOVER TODAS AS VALIDAÇÕES PROBLEMÁTICAS
            ModelState.Remove("UsuarioId");
            ModelState.Remove("FotoCapa");
            ModelState.Remove("FotoCapaUpload");

            try
            {
                // PROCESSAR UPLOAD DE FOTO
                if (model.FotoCapaUpload != null)
                {
                    model.FotoCapa = await SalvarFotoAsync(model.FotoCapaUpload);
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("FotoCapaUpload", ex.Message);
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validação de datas
            if (model.DataInicio.HasValue && model.DataFim.HasValue && model.DataInicio > model.DataFim)
            {
                ModelState.AddModelError("DataFim", "Data de fim deve ser posterior à data de início");
                return View(model);
            }

            var proposta = new Proposta
            {
                Titulo = model.Titulo,
                UsuarioId = model.UsuarioId.Value,
                DataInicio = model.DataInicio,
                DataFim = model.DataFim,
                NumeroPassageiros = model.NumeroPassageiros,
                NumeroCriancas = model.NumeroCriancas,
                FotoCapa = model.FotoCapa,
                LayoutId = model.LayoutId,
                ObservacoesGerais = model.ObservacoesGerais,
                StatusProposta = StatusProposta.Rascunho,
                LinkPublicoAtivo = model.LinkPublicoAtivo,
                DataExpiracaoLink = model.DataExpiracaoLink,
                DataCriacao = DateTime.Now
            };

            _context.Propostas.Add(proposta);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Proposta criada com sucesso!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
            {
                return NotFound();
            }

            var model = new PropostaViewModel
            {
                Id = proposta.Id,
                Titulo = proposta.Titulo,
                UsuarioId = proposta.UsuarioId,
                DataInicio = proposta.DataInicio,
                DataFim = proposta.DataFim,
                NumeroPassageiros = proposta.NumeroPassageiros,
                NumeroCriancas = proposta.NumeroCriancas,
                FotoCapa = proposta.FotoCapa,
                LayoutId = proposta.LayoutId,
                ObservacoesGerais = proposta.ObservacoesGerais,
                StatusProposta = proposta.StatusProposta,
                LinkPublicoAtivo = proposta.LinkPublicoAtivo,
                DataExpiracaoLink = proposta.DataExpiracaoLink,
                DataCriacao = proposta.DataCriacao,
                DataModificacao = proposta.DataModificacao
            };

            await CarregarDadosParaView();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(PropostaViewModel model)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                await CarregarDadosParaView();
                return View(model);
            }

            var proposta = await _context.Propostas.FindAsync(model.Id);
            if (proposta == null)
            {
                return NotFound();
            }

            // Validação customizada de datas
            if (model.DataInicio.HasValue && model.DataFim.HasValue && model.DataInicio > model.DataFim)
            {
                ModelState.AddModelError("DataFim", "Data de fim deve ser posterior à data de início");
                await CarregarDadosParaView();
                return View(model);
            }

            proposta.Titulo = model.Titulo;
            proposta.DataInicio = model.DataInicio;
            proposta.DataFim = model.DataFim;
            proposta.NumeroPassageiros = model.NumeroPassageiros;
            proposta.NumeroCriancas = model.NumeroCriancas;
            proposta.FotoCapa = model.FotoCapa;
            proposta.LayoutId = model.LayoutId;
            proposta.ObservacoesGerais = model.ObservacoesGerais;
            proposta.StatusProposta = model.StatusProposta;
            proposta.LinkPublicoAtivo = model.LinkPublicoAtivo;
            proposta.DataExpiracaoLink = model.DataExpiracaoLink;
            proposta.DataModificacao = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Proposta atualizada com sucesso!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AlterarStatus(Guid id, StatusProposta status)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var proposta = await _context.Propostas.FindAsync(id);
            if (proposta == null)
            {
                return NotFound();
            }

            var statusAnterior = proposta.StatusProposta;
            proposta.StatusProposta = status;
            proposta.DataModificacao = DateTime.Now;

            await _context.SaveChangesAsync();

            string mensagem = status switch
            {
                StatusProposta.Rascunho => "Proposta marcada como rascunho",
                StatusProposta.Enviada => "Proposta enviada com sucesso!",
                StatusProposta.Aprovada => "Proposta aprovada! 🎉",
                StatusProposta.Rejeitada => "Proposta rejeitada",
                StatusProposta.Cancelada => "Proposta cancelada",
                _ => "Status alterado com sucesso!"
            };

            TempData["Sucesso"] = mensagem;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Publico(Guid id)
        {
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
            {
                return NotFound("Proposta não encontrada");
            }

            // Verificar se link está ativo
            if (!proposta.LinkPublicoAtivo)
            {
                return BadRequest("Link público não está ativo para esta proposta");
            }

            // Verificar se link não expirou
            if (proposta.DataExpiracaoLink.HasValue && proposta.DataExpiracaoLink < DateTime.Now)
            {
                return BadRequest("Link público expirado");
            }

            // Limpar ViewBag para evitar erros
            ViewBag.Title = proposta.Titulo;

            return View(proposta);
        }


        [HttpGet]
        public async Task<IActionResult> Detalhes(Guid id)
        {
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
            {
                return NotFound();
            }

            return View(proposta);
        }

        private async Task CarregarDadosParaView()
        {
            var layouts = await _context.Layouts
                .Where(l => l.Ativo)
                .OrderBy(l => l.Nome)
                .ToListAsync();

            ViewBag.Layouts = layouts;
        }
        
        [HttpPost]
        public async Task<IActionResult> AlterarLink(Guid id, bool ativo)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var proposta = await _context.Propostas.FindAsync(id);
            if (proposta == null)
            {
                return NotFound();
            }

            proposta.LinkPublicoAtivo = ativo;
            proposta.DataModificacao = DateTime.Now;

            // Se estiver ativando e não tem data de expiração, definir para 30 dias
            if (ativo && !proposta.DataExpiracaoLink.HasValue)
            {
                proposta.DataExpiracaoLink = DateTime.Now.AddDays(30);
            }

            await _context.SaveChangesAsync();

            string mensagem = ativo ? "Link público ativado com sucesso!" : "Link público desativado";
            TempData["Sucesso"] = mensagem;

            return RedirectToAction("Index");
        }

    }
}