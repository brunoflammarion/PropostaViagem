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

            // REMOVER VALIDAÇÕES PROBLEMÁTICAS
            ModelState.Remove("UsuarioId");
            ModelState.Remove("FotoCapa");
            ModelState.Remove("FotoCapaUpload");

            try
            {
                // PROCESSAR UPLOAD DE FOTO APENAS SE FORNECIDA
                if (model.FotoCapaUpload != null && model.FotoCapaUpload.Length > 0)
                {
                    model.FotoCapa = await SalvarFotoAsync(model.FotoCapaUpload);
                }
                else
                {
                    // Se não há upload, FotoCapa permanece null
                    model.FotoCapa = null;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("FotoCapaUpload", ex.Message);
                await CarregarDadosParaView();
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                await CarregarDadosParaView();
                return View(model);
            }

            // Validação de datas
            if (model.DataInicio.HasValue && model.DataFim.HasValue && model.DataInicio > model.DataFim)
            {
                ModelState.AddModelError("DataFim", "Data de fim deve ser posterior à data de início");
                await CarregarDadosParaView();
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
                FotoCapa = model.FotoCapa, // Pode ser null
                LayoutId = model.LayoutId,
                ObservacoesGerais = string.IsNullOrWhiteSpace(model.ObservacoesGerais) ? null : model.ObservacoesGerais.Trim(), // Limpar string vazia para null
                StatusProposta = StatusProposta.Rascunho,
                LinkPublicoAtivo = model.LinkPublicoAtivo,
                DataExpiracaoLink = model.DataExpiracaoLink,
                DataCriacao = DateTime.Now
            };

            _context.Propostas.Add(proposta);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Proposta criada com sucesso! Agora você pode adicionar destinos e fotos.";

            // REDIRECIONAR DIRETO PARA GERENCIAR DESTINOS
            return RedirectToAction("Gerenciar", "Destino", new { propostaId = proposta.Id });
        }

        // Método Editar GET - Corrigido para usar o mesmo formato da criação
        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .Include(p => p.Destinos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
            {
                return NotFound();
            }

            // Verificar se o usuário logado é o dono da proposta
            var usuarioLogadoId = ObterUsuarioLogadoId();
            if (proposta.UsuarioId != usuarioLogadoId)
            {
                TempData["Erro"] = "Você não tem permissão para editar esta proposta.";
                return RedirectToAction("Index");
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

        // Método Editar POST - Corrigido para tratar upload de foto
        [HttpPost]
        public async Task<IActionResult> Editar(PropostaViewModel model, string acao = "salvar")
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            // Verificar se a proposta existe e pertence ao usuário
            var proposta = await _context.Propostas.FindAsync(model.Id);
            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index");
            }

            var usuarioLogadoId = ObterUsuarioLogadoId();
            if (proposta.UsuarioId != usuarioLogadoId)
            {
                TempData["Erro"] = "Você não tem permissão para editar esta proposta.";
                return RedirectToAction("Index");
            }

            // SE A AÇÃO FOR "gerenciar_destinos", REDIRECIONAR DIRETO
            if (acao == "gerenciar_destinos")
            {
                // Salvar primeiro se houver mudanças básicas
                try
                {
                    await SalvarAlteracoesProposta(model);
                    return RedirectToAction("Gerenciar", "Destino", new { propostaId = model.Id });
                }
                catch (Exception ex)
                {
                    TempData["Erro"] = ex.Message;
                    await CarregarDadosParaView();
                    return View(model);
                }
            }

            // REMOVER VALIDAÇÕES PROBLEMÁTICAS
            ModelState.Remove("UsuarioId");
            ModelState.Remove("FotoCapa");
            ModelState.Remove("FotoCapaUpload");

            try
            {
                // PROCESSAR UPLOAD DE NOVA FOTO SE FORNECIDA
                if (model.FotoCapaUpload != null && model.FotoCapaUpload.Length > 0)
                {
                    // Excluir foto anterior se existir
                    if (!string.IsNullOrEmpty(proposta.FotoCapa))
                    {
                        try
                        {
                            var caminhoAnterior = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", proposta.FotoCapa.TrimStart('/'));
                            if (System.IO.File.Exists(caminhoAnterior))
                            {
                                System.IO.File.Delete(caminhoAnterior);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao excluir foto anterior: {ex.Message}");
                        }
                    }

                    // Salvar nova foto
                    model.FotoCapa = await SalvarFotoAsync(model.FotoCapaUpload);
                }
                else if (string.IsNullOrEmpty(model.FotoCapa))
                {
                    // Se não há upload e o campo está vazio, manter a foto atual
                    model.FotoCapa = proposta.FotoCapa;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("FotoCapaUpload", ex.Message);
                await CarregarDadosParaView();
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                await CarregarDadosParaView();
                return View(model);
            }

            try
            {
                await SalvarAlteracoesProposta(model);
                TempData["Sucesso"] = "Proposta atualizada com sucesso!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = ex.Message;
                await CarregarDadosParaView();
                return View(model);
            }
        }

        // Método auxiliar corrigido
        private async Task SalvarAlteracoesProposta(PropostaViewModel model)
        {
            var proposta = await _context.Propostas.FindAsync(model.Id);
            if (proposta == null)
                throw new InvalidOperationException("Proposta não encontrada");

            // Validação customizada de datas
            if (model.DataInicio.HasValue && model.DataFim.HasValue && model.DataInicio > model.DataFim)
            {
                throw new InvalidOperationException("Data de fim deve ser posterior à data de início");
            }

            proposta.Titulo = model.Titulo;
            proposta.DataInicio = model.DataInicio;
            proposta.DataFim = model.DataFim;
            proposta.NumeroPassageiros = model.NumeroPassageiros;
            proposta.NumeroCriancas = model.NumeroCriancas;
            proposta.FotoCapa = model.FotoCapa;
            proposta.LayoutId = model.LayoutId;
            proposta.ObservacoesGerais = string.IsNullOrWhiteSpace(model.ObservacoesGerais) ? null : model.ObservacoesGerais.Trim();
            proposta.StatusProposta = model.StatusProposta;
            proposta.LinkPublicoAtivo = model.LinkPublicoAtivo;
            proposta.DataExpiracaoLink = model.DataExpiracaoLink;
            proposta.DataModificacao = DateTime.Now;

            await _context.SaveChangesAsync();
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
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
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
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
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

        [HttpPost]
        public async Task<IActionResult> Excluir(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
            {
                return NotFound();
            }

            // Remover arquivos físicos das fotos dos destinos
            foreach (var destino in proposta.Destinos)
            {
                foreach (var foto in destino.Fotos)
                {
                    if (!string.IsNullOrEmpty(foto.CaminhoFoto))
                    {
                        try
                        {
                            var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.CaminhoFoto.TrimStart('/'));
                            if (System.IO.File.Exists(caminhoCompleto))
                            {
                                System.IO.File.Delete(caminhoCompleto);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log do erro mas continua com a exclusão
                            Console.WriteLine($"Erro ao excluir arquivo de foto: {ex.Message}");
                        }
                    }
                }
            }

            // Remover foto de capa da proposta
            if (!string.IsNullOrEmpty(proposta.FotoCapa))
            {
                try
                {
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", proposta.FotoCapa.TrimStart('/'));
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        System.IO.File.Delete(caminhoCompleto);
                    }
                }
                catch (Exception ex)
                {
                    // Log do erro mas continua com a exclusão
                    Console.WriteLine($"Erro ao excluir foto de capa: {ex.Message}");
                }
            }

            _context.Propostas.Remove(proposta);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Proposta excluída com sucesso!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GerenciarDestinos(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            return RedirectToAction("Gerenciar", "Destino", new { propostaId = id });
        }

        // API ENDPOINTS PARA AJAX
        [HttpGet("api/proposta/{propostaId}/destinos")]
        public async Task<IActionResult> GetDestinos(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var proposta = await _context.Propostas
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            var destinos = proposta.Destinos.Select(d => new
            {
                id = d.Id,
                nome = d.Nome,
                descricao = d.Descricao,
                cidade = d.Cidade,
                pais = d.Pais,
                dataChegada = d.DataChegada?.ToString("yyyy-MM-dd"),
                dataSaida = d.DataSaida?.ToString("yyyy-MM-dd"),
                ordem = d.Ordem,
                fotos = d.Fotos.Select(f => new
                {
                    id = f.Id,
                    caminhoFoto = f.CaminhoFoto,
                    descricao = f.Descricao,
                    principal = f.Principal,
                    ordem = f.Ordem
                }).ToList()
            }).ToList();

            return Json(destinos);
        }

        [HttpGet("api/proposta/{propostaId}/resumo")]
        public async Task<IActionResult> GetResumo(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            var resumo = new
            {
                id = proposta.Id,
                titulo = proposta.Titulo,
                status = proposta.StatusProposta.ToString(),
                linkPublicoAtivo = proposta.LinkPublicoAtivo,
                totalDestinos = proposta.Destinos.Count,
                totalFotos = proposta.Destinos.Sum(d => d.Fotos.Count),
                dataInicio = proposta.DataInicio?.ToString("yyyy-MM-dd"),
                dataFim = proposta.DataFim?.ToString("yyyy-MM-dd"),
                numeroPassageiros = proposta.NumeroPassageiros,
                numeroCriancas = proposta.NumeroCriancas
            };

            return Json(resumo);
        }

        [HttpPost("api/proposta/{propostaId}/status")]
        public async Task<IActionResult> AlterarStatusAjax(Guid propostaId, [FromBody] AlterarStatusRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            proposta.StatusProposta = request.Status;
            proposta.DataModificacao = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Status alterado com sucesso!" });
        }

        [HttpPost("api/proposta/{propostaId}/link")]
        public async Task<IActionResult> AlterarLinkAjax(Guid propostaId, [FromBody] AlterarLinkRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            proposta.LinkPublicoAtivo = request.Ativo;
            proposta.DataModificacao = DateTime.Now;

            // Se estiver ativando e não tem data de expiração, definir para 30 dias
            if (request.Ativo && !proposta.DataExpiracaoLink.HasValue)
            {
                proposta.DataExpiracaoLink = DateTime.Now.AddDays(30);
            }

            await _context.SaveChangesAsync();

            var linkPublico = request.Ativo ?
                $"{Request.Scheme}://{Request.Host}/Proposta/Publico/{propostaId}" : null;

            return Json(new
            {
                success = true,
                message = request.Ativo ? "Link público ativado!" : "Link público desativado!",
                linkPublico = linkPublico
            });
        }

        // Endpoint para obter estatísticas rápidas
        [HttpGet("api/proposta/{propostaId}/stats")]
        public async Task<IActionResult> GetEstatisticas(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .Include(p => p.PropostaVisualizacoes)
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (proposta == null)
                return NotFound();

            var stats = new
            {
                totalDestinos = proposta.Destinos.Count,
                totalFotos = proposta.Destinos.Sum(d => d.Fotos.Count),
                totalVisualizacoes = proposta.PropostaVisualizacoes.Count,
                ultimaVisualizacao = proposta.PropostaVisualizacoes
                    .OrderByDescending(v => v.DataHoraInicio)
                    .FirstOrDefault()?.DataHoraInicio.ToString("dd/MM/yyyy HH:mm"),
                tempoMedioVisualizacao = proposta.PropostaVisualizacoes.Any() ?
                    Math.Round(proposta.PropostaVisualizacoes.Average(v => v.TempoVisualizacaoSegundos)) : 0,
                taxaInteracao = proposta.PropostaVisualizacoes.Any() ?
                    Math.Round((double)proposta.PropostaVisualizacoes.Count(v => v.ClicouEmail || v.ClicouWhatsApp) /
                               proposta.PropostaVisualizacoes.Count * 100, 1) : 0
            };

            return Json(stats);
        }

        // Endpoint para duplicar proposta
        [HttpPost("api/proposta/{propostaId}/duplicar")]
        public async Task<IActionResult> DuplicarProposta(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var propostaOriginal = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioId == usuarioId);

            if (propostaOriginal == null)
                return NotFound();

            var novaProposta = new Proposta
            {
                Id = Guid.NewGuid(),
                Titulo = $"[CÓPIA] {propostaOriginal.Titulo}",
                UsuarioId = usuarioId,
                DataInicio = propostaOriginal.DataInicio,
                DataFim = propostaOriginal.DataFim,
                NumeroPassageiros = propostaOriginal.NumeroPassageiros,
                NumeroCriancas = propostaOriginal.NumeroCriancas,
                FotoCapa = propostaOriginal.FotoCapa,
                LayoutId = propostaOriginal.LayoutId,
                ObservacoesGerais = propostaOriginal.ObservacoesGerais,
                StatusProposta = StatusProposta.Rascunho,
                LinkPublicoAtivo = false,
                DataCriacao = DateTime.Now
            };

            _context.Propostas.Add(novaProposta);

            // Duplicar destinos
            foreach (var destinoOriginal in propostaOriginal.Destinos.OrderBy(d => d.Ordem))
            {
                var novoDestino = new Destino
                {
                    Id = Guid.NewGuid(),
                    PropostaId = novaProposta.Id,
                    Nome = destinoOriginal.Nome,
                    Descricao = destinoOriginal.Descricao,
                    DataChegada = destinoOriginal.DataChegada,
                    DataSaida = destinoOriginal.DataSaida,
                    Ordem = destinoOriginal.Ordem,
                    Pais = destinoOriginal.Pais,
                    Cidade = destinoOriginal.Cidade,
                    DataCriacao = DateTime.Now
                };

                _context.Destinos.Add(novoDestino);

                // Duplicar fotos (mantendo referências aos mesmos arquivos)
                foreach (var fotoOriginal in destinoOriginal.Fotos.OrderBy(f => f.Ordem))
                {
                    var novaFoto = new DestinoFoto
                    {
                        Id = Guid.NewGuid(),
                        DestinoId = novoDestino.Id,
                        CaminhoFoto = fotoOriginal.CaminhoFoto,
                        Descricao = fotoOriginal.Descricao,
                        Ordem = fotoOriginal.Ordem,
                        Principal = fotoOriginal.Principal,
                        DataCriacao = DateTime.Now
                    };

                    _context.DestinoFotos.Add(novaFoto);
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Proposta duplicada com sucesso!",
                novaPropostaId = novaProposta.Id,
                redirect = Url.Action("Editar", new { id = novaProposta.Id })
            });
        }

        // Endpoint para preview da proposta
        [HttpGet("api/proposta/{propostaId}/preview")]
        public async Task<IActionResult> GetPreview(Guid propostaId)
        {
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.Where(f => f.Principal).OrderBy(f => f.Ordem))
                .FirstOrDefaultAsync(p => p.Id == propostaId);

            if (proposta == null)
                return NotFound();

            // Verificar se é pública ou se o usuário tem acesso
            if (!proposta.LinkPublicoAtivo && !UsuarioLogado())
                return Unauthorized();

            if (!proposta.LinkPublicoAtivo && UsuarioLogado())
            {
                var usuarioId = ObterUsuarioLogadoId();
                if (proposta.UsuarioId != usuarioId)
                    return Forbid();
            }

            var preview = new
            {
                id = proposta.Id,
                titulo = proposta.Titulo,
                periodo = new
                {
                    inicio = proposta.DataInicio?.ToString("dd/MM/yyyy"),
                    fim = proposta.DataFim?.ToString("dd/MM/yyyy"),
                    dias = proposta.DataInicio.HasValue && proposta.DataFim.HasValue ?
                        (proposta.DataFim.Value - proposta.DataInicio.Value).Days + 1 : (int?)null
                },
                passageiros = new
                {
                    adultos = proposta.NumeroPassageiros,
                    criancas = proposta.NumeroCriancas,
                    total = proposta.NumeroPassageiros + proposta.NumeroCriancas
                },
                fotoCapa = proposta.FotoCapa,
                observacoes = proposta.ObservacoesGerais,
                destinos = proposta.Destinos.Select(d => new
                {
                    id = d.Id,
                    nome = d.Nome,
                    cidade = d.Cidade,
                    pais = d.Pais,
                    fotoPrincipal = d.Fotos.FirstOrDefault()?.CaminhoFoto,
                    totalFotos = d.Fotos.Count,
                    ordem = d.Ordem
                }).ToList(),
                organizador = new
                {
                    nome = proposta.Usuario.Nome,
                    email = proposta.Usuario.Email,
                    telefone = proposta.Usuario.Telefone
                },
                status = new
                {
                    proposta = proposta.StatusProposta.ToString(),
                    linkPublico = proposta.LinkPublicoAtivo,
                    dataExpiracao = proposta.DataExpiracaoLink?.ToString("dd/MM/yyyy HH:mm")
                }
            };

            return Json(preview);
        }
        // Método para processar a remoção de foto atual via AJAX
        [HttpPost]
        public async Task<IActionResult> RemoverFotoAtual(Guid id)
        {
            if (!UsuarioLogado())
                return Json(new { success = false, message = "Usuário não logado" });

            var usuarioId = ObterUsuarioLogadoId();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId);

            if (proposta == null)
                return Json(new { success = false, message = "Proposta não encontrada" });

            // Remover arquivo físico se existir
            if (!string.IsNullOrEmpty(proposta.FotoCapa))
            {
                try
                {
                    var caminhoCompleto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", proposta.FotoCapa.TrimStart('/'));
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        System.IO.File.Delete(caminhoCompleto);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao excluir arquivo de foto: {ex.Message}");
                }
            }

            // Atualizar no banco
            proposta.FotoCapa = null;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Foto removida com sucesso" });
        }

        
        // Método para validar permissões do usuário
        private async Task<bool> UsuarioTemPermissao(Guid propostaId)
        {
            if (!UsuarioLogado())
                return false;

            var usuarioId = ObterUsuarioLogadoId();
            var proposta = await _context.Propostas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == propostaId);

            return proposta != null && proposta.UsuarioId == usuarioId;
        }

       
        // Método para obter estatísticas da proposta (para o preview)
        [HttpGet]
        public async Task<IActionResult> ObterEstatisticasProposta(Guid id)
        {
            if (!UsuarioLogado())
                return Json(new { success = false, message = "Não autorizado" });

            if (!await UsuarioTemPermissao(id))
                return Json(new { success = false, message = "Sem permissão" });

            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
                return Json(new { success = false, message = "Proposta não encontrada" });

            var estatisticas = new
            {
                totalDestinos = proposta.Destinos.Count,
                temFotoCapa = !string.IsNullOrEmpty(proposta.FotoCapa),
                temObservacoes = !string.IsNullOrEmpty(proposta.ObservacoesGerais),
                linkPublicoAtivo = proposta.LinkPublicoAtivo,
                status = proposta.StatusProposta.ToString(),
                duracaoDias = proposta.DataInicio.HasValue && proposta.DataFim.HasValue
                    ? (proposta.DataFim.Value - proposta.DataInicio.Value).Days + 1
                    : (int?)null
            };

            return Json(new { success = true, data = estatisticas });
        }
    }

    // DTOs para as requisições AJAX
    public class AlterarStatusRequest
    {
        public StatusProposta Status { get; set; }
    }

    public class AlterarLinkRequest
    {
        public bool Ativo { get; set; }
        public DateTime? DataExpiracao { get; set; }
    }
}