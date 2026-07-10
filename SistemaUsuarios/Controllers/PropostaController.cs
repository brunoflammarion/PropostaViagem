using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Controllers
{
    public class PropostaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ITarefaService _tarefaService;
        private readonly BlobStorageService _blob;

        public PropostaController(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration, ITarefaService tarefaService, BlobStorageService blob)
        {
            _context      = context;
            _blob         = blob;
            _cache        = cache;
            _configuration = configuration;
            _tarefaService = tarefaService;
        }

        // ─── Código de acesso ─────────────────────────────────────────────────────
        private static string GerarCodigoCurto()
        {
            // Formato: 3 letras + dash + 3 dígitos → "MAR-724"
            // Evita chars confusos: sem I, O (letras) e sem 0, 1 (dígitos)
            const string letras  = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digitos = "23456789";
            var rng = new Random();
            var l = new string(Enumerable.Range(0, 3).Select(_ => letras[rng.Next(letras.Length)]).ToArray());
            var d = new string(Enumerable.Range(0, 3).Select(_ => digitos[rng.Next(digitos.Length)]).ToArray());
            return $"{l}-{d}";
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        /// <summary>
        /// Verifica se o usuário logado tem autorização sobre a proposta informada.
        /// Master: acesso a qualquer proposta onde UsuarioMasterId == seu Id.
        /// Associado: acesso apenas às propostas onde UsuarioResponsavelId == seu Id.
        /// </summary>
        private bool PropostaAutorizada(Proposta p, Guid usuarioLogadoId)
        {
            if (SessaoIsMaster())
                return p.UsuarioMasterId == usuarioLogadoId;
            return p.UsuarioResponsavelId == usuarioLogadoId;
        }

        private Task<string> SalvarFotoAsync(IFormFile foto)
            => _blob.SalvarAsync(foto, "propostas");

        private Guid ObterUsuarioLogadoId()
        {
            var usuarioIdString = HttpContext.Session.GetString("UsuarioId");
            return Guid.Parse(usuarioIdString);
        }

        public async Task<IActionResult> Index(PropostaFiltroViewModel filtro)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            // Carregar preferência de visualização do usuário
            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId);
            filtro.VisualizacaoAtual = usuario?.PreferenciaVisualizacao ?? "lista";

            var tipoUsuario = usuario?.TipoUsuario ?? TipoUsuario.Master;
            filtro.IsMaster = tipoUsuario == TipoUsuario.Master;

            // Usuários visíveis no dropdown: Master vê a si mesmo + associados; Associado vê só a si
            if (tipoUsuario == TipoUsuario.Master)
                filtro.Usuarios = await _context.Usuarios
                    .Where(u => u.Id == usuarioId || u.UsuarioMasterId == usuarioId)
                    .OrderBy(u => u.Nome).ToListAsync();
            else
                filtro.Usuarios = new List<Usuario> { usuario! };

            // Query base: escopo por hierarquia
            IQueryable<Proposta> query;
            if (tipoUsuario == TipoUsuario.Master)
            {
                // Master vê todas as propostas da equipe (UsuarioMasterId == seu Id)
                query = _context.Propostas
                    .Include(p => p.Usuario)
                    .Include(p => p.UsuarioResponsavel)
                    .Where(p => p.UsuarioMasterId == usuarioId);
            }
            else
            {
                // Associado vê apenas as propostas das quais é responsável
                query = _context.Propostas
                    .Include(p => p.Usuario)
                    .Include(p => p.UsuarioResponsavel)
                    .Where(p => p.UsuarioResponsavelId == usuarioId);
            }

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
                NomeResponsavel = p.UsuarioResponsavel?.Nome ?? p.Usuario?.Nome ?? "N/A",
                UsuarioId = p.UsuarioId,
                UsuarioResponsavelId = p.UsuarioResponsavelId,
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

            // Determinar o UsuarioMasterId: para Master é o próprio; para Associado é o master da sessão
            var tipoStr = HttpContext.Session.GetString("TipoUsuario");
            var isMaster = tipoStr != "Associado";
            Guid? masterIdParaProposta;
            if (isMaster)
                masterIdParaProposta = model.UsuarioId.Value;
            else if (Guid.TryParse(HttpContext.Session.GetString("UsuarioMasterId"), out var mId))
                masterIdParaProposta = mId;
            else
                masterIdParaProposta = model.UsuarioId.Value;

            var proposta = new Proposta
            {
                Titulo = model.Titulo,
                UsuarioId = model.UsuarioId.Value,
                UsuarioMasterId = masterIdParaProposta,
                UsuarioResponsavelId = model.UsuarioId.Value,  // criador é o responsável inicial
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

            TempData["Sucesso"] = "Proposta criada com sucesso! Agora adicione os voos da viagem.";
            TempData["ActiveTab"] = "aereo";
            return RedirectToAction("Editar", new { id = proposta.Id });
        }

        // Método Editar GET
        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioLogadoId = ObterUsuarioLogadoId();

            // Carrega proposta completa (com todos os includes para as abas Destinos e Aéreo)
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.UsuarioResponsavel)
                .Include(p => p.Layout)
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens.OrderBy(h => h.Ordem))
                        .ThenInclude(h => h.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes.OrderBy(a => a.Ordem))
                            .ThenInclude(a => a.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes)
                            .ThenInclude(a => a.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Experiencias.OrderBy(e => e.Ordem))
                        .ThenInclude(e => e.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Experiencias)
                        .ThenInclude(e => e.Arquivos.OrderBy(a => a.DataCriacao))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes.OrderBy(t => t.Ordem))
                        .ThenInclude(t => t.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes)
                        .ThenInclude(t => t.Documentos.OrderBy(d => d.DataCriacao))
                .Include(p => p.Voos.OrderBy(v => v.Ordem))
                    .ThenInclude(v => v.Passageiros.OrderBy(p => p.DataCriacao))
                .Include(p => p.Voos)
                    .ThenInclude(v => v.Anexos.OrderBy(a => a.DataCriacao))
                .Include(p => p.Seguros.OrderBy(s => s.Ordem))
                    .ThenInclude(s => s.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Seguros)
                    .ThenInclude(s => s.Documentos.OrderBy(d => d.DataCriacao))
                .Include(p => p.Cliente)
                .Include(p => p.PassageirosProposta.OrderBy(pp => pp.Ordem))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
                return NotFound();

            // Verificar autorização hierárquica
            if (!PropostaAutorizada(proposta, usuarioLogadoId))
            {
                TempData["Erro"] = "Você não tem permissão para editar esta proposta.";
                return RedirectToAction("Index");
            }

            // Para masters: carregar membros da equipe para o painel de transferência
            if (SessaoIsMaster())
            {
                ViewBag.MembrosEquipe = await _context.Usuarios
                    .Where(u => u.Id == usuarioLogadoId || u.UsuarioMasterId == usuarioLogadoId)
                    .Where(u => u.Status == StatusUsuario.Ativo)
                    .OrderBy(u => u.Nome)
                    .Select(u => new { u.Id, u.Nome, u.TipoUsuario })
                    .ToListAsync();
            }
            ViewBag.IsMaster = SessaoIsMaster();

            var model = new PropostaViewModel
            {
                Id = proposta.Id,
                Titulo = proposta.Titulo,
                UsuarioId = proposta.UsuarioId,
                UsuarioResponsavelId = proposta.UsuarioResponsavelId,
                DataInicio = proposta.DataInicio,
                DataFim = proposta.DataFim,
                NumeroPassageiros = proposta.NumeroPassageiros,
                NumeroCriancas = proposta.NumeroCriancas,
                FotoCapa = proposta.FotoCapa,
                LayoutId = proposta.LayoutId,
                ObservacoesGerais = proposta.ObservacoesGerais,
                ResumoProposta = proposta.ResumoProposta,
                StatusProposta = proposta.StatusProposta,
                LinkPublicoAtivo = proposta.LinkPublicoAtivo,
                DataExpiracaoLink = proposta.DataExpiracaoLink,
                DataCriacao = proposta.DataCriacao,
                DataModificacao = proposta.DataModificacao,
                SolicitarAvaliacaoHospedagem  = proposta.SolicitarAvaliacaoHospedagem,
                SolicitarAvaliacaoAcomodacao  = proposta.SolicitarAvaliacaoAcomodacao,
                SolicitarAvaliacaoExperiencia = proposta.SolicitarAvaliacaoExperiencia,
                NomeCriador    = proposta.Usuario?.Nome,
                NomeResponsavel = proposta.UsuarioResponsavel?.Nome ?? proposta.Usuario?.Nome
            };

            ViewBag.Proposta    = proposta;
            ViewBag.ActiveTab   = TempData["ActiveTab"]?.ToString() ?? "dados";
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
            if (!PropostaAutorizada(proposta, usuarioLogadoId))
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
                    TempData["ActiveTab"] = "destinos";
                    return RedirectToAction("Editar", new { id = model.Id });
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
                    // Excluir foto anterior
                    _ = _blob.DeletarAsync(proposta.FotoCapa);

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
                TempData["ActiveTab"] = "dados";
                return RedirectToAction("Editar", new { id = model.Id });
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
            proposta.ResumoProposta = string.IsNullOrWhiteSpace(model.ResumoProposta) ? null : model.ResumoProposta;
            proposta.StatusProposta = model.StatusProposta;
            proposta.LinkPublicoAtivo = model.LinkPublicoAtivo;
            proposta.DataExpiracaoLink = model.DataExpiracaoLink;
            proposta.SolicitarAvaliacaoHospedagem  = model.SolicitarAvaliacaoHospedagem;
            proposta.SolicitarAvaliacaoAcomodacao  = model.SolicitarAvaliacaoAcomodacao;
            proposta.SolicitarAvaliacaoExperiencia = model.SolicitarAvaliacaoExperiencia;
            proposta.DataModificacao = DateTime.Now;

            // Gera código de acesso automaticamente ao ativar o link pela primeira vez
            if (model.LinkPublicoAtivo && string.IsNullOrEmpty(proposta.CodigoAcesso))
                proposta.CodigoAcesso = GerarCodigoCurto();

            await _context.SaveChangesAsync();
        }

        /// <summary>Salva apenas o ResumoProposta via AJAX (auto-save do editor rico na aba Revisão).</summary>
        [HttpPost]
        public async Task<IActionResult> SalvarResumoProposta(Guid id, string? conteudo)
        {
            if (!UsuarioLogado())
                return Json(new { ok = false, erro = "Não autenticado" });

            var proposta = await _context.Propostas.FindAsync(id);
            if (proposta == null || proposta.UsuarioId != ObterUsuarioLogadoId())
                return Json(new { ok = false, erro = "Proposta não encontrada" });

            proposta.ResumoProposta = string.IsNullOrWhiteSpace(conteudo) ? null : conteudo;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { ok = true });
        }

        /// <summary>Salva apenas o CondicoesPropostaHtml via AJAX (auto-save do editor rico na aba Condições).</summary>
        [HttpPost]
        public async Task<IActionResult> SalvarCondicoesProposta(Guid id, string? conteudo)
        {
            if (!UsuarioLogado())
                return Json(new { ok = false, erro = "Não autenticado" });

            var proposta = await _context.Propostas.FindAsync(id);
            if (proposta == null || proposta.UsuarioId != ObterUsuarioLogadoId())
                return Json(new { ok = false, erro = "Proposta não encontrada" });

            proposta.CondicoesPropostaHtml = string.IsNullOrWhiteSpace(conteudo) ? null : conteudo;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { ok = true });
        }

        /// <summary>Salva apenas o ValoresPropostaHtml via AJAX (auto-save do editor rico na aba Condições).</summary>
        [HttpPost]
        public async Task<IActionResult> SalvarValoresProposta(Guid id, string? conteudo)
        {
            if (!UsuarioLogado())
                return Json(new { ok = false, erro = "Não autenticado" });

            var proposta = await _context.Propostas.FindAsync(id);
            if (proposta == null || proposta.UsuarioId != ObterUsuarioLogadoId())
                return Json(new { ok = false, erro = "Proposta não encontrada" });

            proposta.ValoresPropostaHtml = string.IsNullOrWhiteSpace(conteudo) ? null : conteudo;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { ok = true });
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

            // Gerar tarefas automáticas ao fechar a viagem (idempotente)
            if (status == StatusProposta.Aprovada && statusAnterior != StatusProposta.Aprovada)
            {
                try { await _tarefaService.GerarTarefasParaViagemAsync(id); }
                catch (Exception ex) { /* não bloqueia o fluxo principal */
                    HttpContext.RequestServices.GetRequiredService<ILogger<PropostaController>>()
                        .LogError(ex, "Erro ao gerar tarefas para proposta {PropostaId}", id);
                }
            }

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
            // Validação leve antes de carregar tudo
            var meta = await _context.Propostas
                .Where(p => p.Id == id)
                .Select(p => new { p.Id, p.LinkPublicoAtivo, p.DataExpiracaoLink, p.CodigoAcesso, p.Titulo })
                .FirstOrDefaultAsync();

            if (meta == null)
                return NotFound();

            if (!meta.LinkPublicoAtivo)
            {
                ViewBag.Mensagem = "Esta proposta não está disponível ao público no momento.";
                ViewBag.Subtitulo = "O link pode ter sido desativado pelo agente responsável.";
                return View("PropostaIndisponivel");
            }

            if (meta.DataExpiracaoLink.HasValue && meta.DataExpiracaoLink.Value < DateTime.Now)
            {
                ViewBag.Mensagem = "Esta proposta expirou e não está mais disponível.";
                ViewBag.Subtitulo = "Entre em contato com o agente para receber uma nova proposta atualizada.";
                return View("PropostaIndisponivel");
            }

            // Portão de código de acesso
            if (!string.IsNullOrEmpty(meta.CodigoAcesso))
            {
                var sessionKey = $"proposta_acesso_{id}";
                if (HttpContext.Session.GetString(sessionKey) != "ok")
                    return RedirectToAction("AcessoProposta", new { id });
            }

            // Carga completa
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .Include(p => p.Cliente)
                .Include(p => p.PassageirosProposta.OrderBy(pp => pp.Ordem))
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens.OrderBy(h => h.Ordem))
                        .ThenInclude(h => h.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes.OrderBy(a => a.Ordem))
                            .ThenInclude(a => a.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes)
                            .ThenInclude(a => a.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Experiencias.OrderBy(e => e.Ordem))
                        .ThenInclude(e => e.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes.OrderBy(t => t.Ordem))
                        .ThenInclude(t => t.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes)
                        .ThenInclude(t => t.Documentos.OrderBy(d => d.DataCriacao))
                .Include(p => p.Voos.OrderBy(v => v.Ordem))
                    .ThenInclude(v => v.Passageiros.OrderBy(pv => pv.DataCriacao))
                .Include(p => p.Seguros.OrderBy(s => s.Ordem))
                    .ThenInclude(s => s.Imagens.OrderBy(i => i.Ordem))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
                return NotFound();

            var avaliacoes = await _context.AvaliacoesCliente
                .AsNoTracking()
                .Where(a => a.PropostaId == id)
                .ToListAsync();
            ViewBag.AvaliacoesPorItem = avaliacoes.ToDictionary(
                a => $"{(int)a.TipoItem}_{a.ItemId}",
                a => a);

            ViewBag.Title = proposta.Titulo;
            ViewBag.GoogleApiKey = _configuration["GoogleApiKey"] ?? "";
            var layoutDispatch = (proposta.Layout?.Nome ?? "Padrão").ToLower();
            if (layoutDispatch.Contains("executivo"))
                return View("PublicoExecutivo", proposta);
            else if (layoutDispatch.Contains("familiar"))
                return View("PublicoFamiliar", proposta);
            return View(proposta);
        }

        // ─── Portão de acesso: solicitar e validar código ─────────────────────────

        [HttpGet]
        public async Task<IActionResult> AcessoProposta(Guid id)
        {
            var meta = await _context.Propostas
                .Where(p => p.Id == id)
                .Select(p => new { p.Id, p.Titulo, p.FotoCapa, p.LinkPublicoAtivo, p.DataExpiracaoLink, p.CodigoAcesso })
                .FirstOrDefaultAsync();

            if (meta == null)
                return NotFound();

            if (!meta.LinkPublicoAtivo)
            {
                ViewBag.Mensagem = "Esta proposta não está disponível ao público no momento.";
                ViewBag.Subtitulo = "";
                return View("PropostaIndisponivel");
            }

            if (meta.DataExpiracaoLink.HasValue && meta.DataExpiracaoLink.Value < DateTime.Now)
            {
                ViewBag.Mensagem = "Esta proposta expirou e não está mais disponível.";
                ViewBag.Subtitulo = "Entre em contato com o agente para receber uma nova proposta atualizada.";
                return View("PropostaIndisponivel");
            }

            // Sem código → acesso direto
            if (string.IsNullOrEmpty(meta.CodigoAcesso))
                return RedirectToAction("Publico", new { id });

            // Já autenticado → acesso direto
            if (HttpContext.Session.GetString($"proposta_acesso_{id}") == "ok")
                return RedirectToAction("Publico", new { id });

            ViewBag.PropostaId = id;
            ViewBag.Titulo = meta.Titulo;
            ViewBag.FotoCapa = meta.FotoCapa;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcessoProposta(Guid id, string? codigo)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var bloqueioKey = $"proposta_bloqueio_{id}_{ip}";
            var falhasKey   = $"proposta_falhas_{id}_{ip}";

            // Carrega nome/foto para re-renderizar view se necessário
            var meta = await _context.Propostas
                .Where(p => p.Id == id)
                .Select(p => new { p.Titulo, p.FotoCapa, p.CodigoAcesso })
                .FirstOrDefaultAsync();

            if (meta == null)
                return NotFound();

            void PrepararViewBag(string? erro = null, bool bloqueado = false)
            {
                ViewBag.PropostaId = id;
                ViewBag.Titulo     = meta.Titulo;
                ViewBag.FotoCapa   = meta.FotoCapa;
                ViewBag.Erro       = erro;
                ViewBag.Bloqueado  = bloqueado;
            }

            // Verificar bloqueio temporário
            if (_cache.TryGetValue(bloqueioKey, out _))
            {
                PrepararViewBag("Muitas tentativas incorretas. Por favor, aguarde alguns minutos e tente novamente.", true);
                return View();
            }

            var codigoNormalizado = (codigo ?? "").Trim().Replace("-", "").ToUpperInvariant();
            var codigoCorreto     = (meta.CodigoAcesso ?? "").Replace("-", "").ToUpperInvariant();

            if (string.Equals(codigoNormalizado, codigoCorreto) && codigoCorreto.Length > 0)
            {
                _cache.Remove(falhasKey);
                HttpContext.Session.SetString($"proposta_acesso_{id}", "ok");
                return RedirectToAction("Publico", new { id });
            }

            // Código incorreto → incrementar contador
            _cache.TryGetValue(falhasKey, out int falhas);
            falhas++;

            const int maxTentativas = 5;
            if (falhas >= maxTentativas)
            {
                _cache.Set(bloqueioKey, true, TimeSpan.FromMinutes(15));
                _cache.Remove(falhasKey);
                PrepararViewBag("Muitas tentativas incorretas. Tente novamente em 15 minutos.", true);
            }
            else
            {
                _cache.Set(falhasKey, falhas, TimeSpan.FromMinutes(15));
                var restantes = maxTentativas - falhas;
                PrepararViewBag($"Código incorreto. Você ainda tem {restantes} tentativa{(restantes == 1 ? "" : "s")}.");
            }

            return View();
        }

        // ─── API: gerar/regenerar código de acesso ────────────────────────────────

        [HttpPost("api/proposta/{propostaId}/codigo-acesso/gerar")]
        public async Task<IActionResult> GerarCodigoAcesso(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var proposta  = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound();

            proposta.CodigoAcesso   = GerarCodigoCurto();
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, codigo = proposta.CodigoAcesso });
        }

        // ─── API: remover código de acesso ────────────────────────────────────────

        [HttpPost("api/proposta/{propostaId}/codigo-acesso/remover")]
        public async Task<IActionResult> RemoverCodigoAcesso(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster = SessaoIsMaster();
            var proposta  = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound();

            proposta.CodigoAcesso   = null;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            // Carga completa — mesmos includes do Publico para o preview ser fiel
            var proposta = await _context.Propostas
                .Include(p => p.Usuario)
                .Include(p => p.Layout)
                .Include(p => p.Cliente)
                .Include(p => p.PassageirosProposta.OrderBy(pp => pp.Ordem))
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens.OrderBy(h => h.Ordem))
                        .ThenInclude(h => h.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes.OrderBy(a => a.Ordem))
                            .ThenInclude(a => a.Fotos.OrderBy(f => f.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Hospedagens)
                        .ThenInclude(h => h.Acomodacoes)
                            .ThenInclude(a => a.Comodidades.OrderBy(c => c.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Experiencias.OrderBy(e => e.Ordem))
                        .ThenInclude(e => e.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes.OrderBy(t => t.Ordem))
                        .ThenInclude(t => t.Imagens.OrderBy(i => i.Ordem))
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Transportes)
                        .ThenInclude(t => t.Documentos.OrderBy(d => d.DataCriacao))
                .Include(p => p.Voos.OrderBy(v => v.Ordem))
                    .ThenInclude(v => v.Passageiros.OrderBy(pv => pv.DataCriacao))
                .Include(p => p.Seguros.OrderBy(s => s.Ordem))
                    .ThenInclude(s => s.Imagens.OrderBy(i => i.Ordem))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null)
                return NotFound();

            if (!PropostaAutorizada(proposta, usuarioId))
                return Forbid();

            var layouts = await _context.Layouts
                .Where(l => l.Ativo)
                .OrderBy(l => l.Nome)
                .ToListAsync();

            ViewBag.Layouts    = layouts;
            ViewBag.IsPreview  = true;
            ViewBag.Title      = proposta.Titulo;
            ViewBag.GoogleApiKey = _configuration["GoogleApiKey"] ?? "";
            ViewBag.LinkPublico = proposta.LinkPublicoAtivo
                ? $"{Request.Scheme}://{Request.Host}/Proposta/Publico/{id}"
                : "";

            return View("Publico", proposta);
        }

        // ─── API: alterar layout da proposta (usado pelo preview do agente) ─────────

        [HttpPost("api/proposta/{propostaId}/layout")]
        public async Task<IActionResult> AlterarLayout(Guid propostaId, [FromBody] AlterarLayoutRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { success = false });

            var usuarioId = ObterUsuarioLogadoId();
            var isMaster  = SessaoIsMaster();

            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound(new { success = false });

            proposta.LayoutId        = request.LayoutId;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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
                    _ = _blob.DeletarAsync(foto.CaminhoFoto);
            }

            // Remover foto de capa da proposta
            _ = _blob.DeletarAsync(proposta.FotoCapa);
            {
            }

            _context.Propostas.Remove(proposta);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Proposta excluída com sucesso!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult GerenciarDestinos(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            TempData["ActiveTab"] = "destinos";
            return RedirectToAction("Editar", new { id });
        }

        // API ENDPOINTS PARA AJAX
        [HttpGet("api/proposta/{propostaId}/destinos")]
        public async Task<IActionResult> GetDestinos(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .Include(p => p.Destinos.OrderBy(d => d.Ordem))
                    .ThenInclude(d => d.Fotos.OrderBy(f => f.Ordem))
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

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

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

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

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

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

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return NotFound();

            proposta.LinkPublicoAtivo = request.Ativo;
            proposta.DataModificacao = DateTime.Now;

            // Se estiver ativando e não tem data de expiração, definir para 30 dias
            if (request.Ativo && !proposta.DataExpiracaoLink.HasValue)
                proposta.DataExpiracaoLink = DateTime.Now.AddDays(30);

            // Gera código de acesso automaticamente ao ativar o link pela primeira vez
            if (request.Ativo && string.IsNullOrEmpty(proposta.CodigoAcesso))
                proposta.CodigoAcesso = GerarCodigoCurto();

            await _context.SaveChangesAsync();

            var linkPublico = request.Ativo
                ? $"{Request.Scheme}://{Request.Host}/Proposta/Publico/{propostaId}"
                : null;

            return Json(new
            {
                success      = true,
                message      = request.Ativo ? "Link público ativado!" : "Link público desativado!",
                linkPublico  = linkPublico,
                codigoAcesso = request.Ativo ? proposta.CodigoAcesso : null
            });
        }

        // Endpoint para obter estatísticas rápidas
        [HttpGet("api/proposta/{propostaId}/stats")]
        public async Task<IActionResult> GetEstatisticas(Guid propostaId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .Include(p => p.PropostaVisualizacoes)
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

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

            var isMaster = SessaoIsMaster();
            var propostaOriginal = await _context.Propostas
                .Include(p => p.Destinos)
                    .ThenInclude(d => d.Fotos)
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (propostaOriginal == null)
                return NotFound();

            // Master do qual a nova cópia pertencerá (mesmo master da original)
            var masterIdNova = isMaster ? usuarioId : propostaOriginal.UsuarioMasterId;

            var novaProposta = new Proposta
            {
                Id = Guid.NewGuid(),
                Titulo = $"[CÓPIA] {propostaOriginal.Titulo}",
                UsuarioId = usuarioId,
                UsuarioMasterId = masterIdNova,
                UsuarioResponsavelId = usuarioId,
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposta == null || !PropostaAutorizada(proposta, usuarioId))
                return Json(new { success = false, message = "Proposta não encontrada ou sem permissão" });

            // Remover arquivo físico se existir
            _ = _blob.DeletarAsync(proposta.FotoCapa);

            // Atualizar no banco
            proposta.FotoCapa = null;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Foto removida com sucesso" });
        }

        
        // Método para validar permissões do usuário
        private async Task<bool> UsuarioTemPermissao(Guid propostaId)
        {
            if (!UsuarioLogado()) return false;
            var usuarioId = ObterUsuarioLogadoId();
            var proposta = await _context.Propostas.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == propostaId);
            return proposta != null && PropostaAutorizada(proposta, usuarioId);
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

        // ─── Transferência de responsável (apenas Master) ───────────────────────
        /// <summary>
        /// Altera o responsável atual de uma proposta para outro membro da equipe.
        /// Regras: apenas Master pode executar; destinatário deve pertencer à mesma equipe.
        /// UsuarioId (criador) nunca é alterado — preserva o histórico de autoria.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirResponsavel(Guid propostaId, Guid novoResponsavelId)
        {
            if (!UsuarioLogado()) return RedirectToAction("Login", "Auth");
            if (!SessaoIsMaster())
            {
                TempData["Erro"] = "Apenas o master pode transferir propostas.";
                return RedirectToAction("Editar", new { id = propostaId });
            }

            var masterId = ObterUsuarioLogadoId();

            // Carrega a proposta garantindo que ela pertence à equipe do master
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId && p.UsuarioMasterId == masterId);

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada ou sem permissão.";
                return RedirectToAction("Index");
            }

            // Valida que o novo responsável pertence à equipe do master
            // (pode ser o próprio master ou um associado com UsuarioMasterId == masterId)
            var novoResponsavel = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == novoResponsavelId
                    && (u.Id == masterId || u.UsuarioMasterId == masterId)
                    && u.Status == StatusUsuario.Ativo);

            if (novoResponsavel == null)
            {
                TempData["Erro"] = "Usuário de destino inválido ou não pertence à sua equipe.";
                return RedirectToAction("Editar", new { id = propostaId });
            }

            // Atualiza apenas o responsável — criador (UsuarioId) permanece intacto
            proposta.UsuarioResponsavelId = novoResponsavelId;
            proposta.DataModificacao = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Proposta transferida para {novoResponsavel.Nome}.";
            return RedirectToAction("Editar", new { id = propostaId });
        }

        // ─── Preferência de visualização ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> SalvarVisualizacao([FromBody] SalvarVisualizacaoRequest req)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
            if (usuario == null) return NotFound();

            usuario.PreferenciaVisualizacao = req.Visualizacao == "kanban" ? "kanban" : "lista";
            await _context.SaveChangesAsync();

            return Ok(new { ok = true });
        }
    }

    // DTOs para as requisições AJAX
    public class AlterarLayoutRequest
    {
        public int? LayoutId { get; set; }
    }

    public class AlterarStatusRequest
    {
        public StatusProposta Status { get; set; }
    }

    public class AlterarLinkRequest
    {
        public bool Ativo { get; set; }
        public DateTime? DataExpiracao { get; set; }
    }

    public class SalvarVisualizacaoRequest
    {
        public string Visualizacao { get; set; } = "lista";
    }
}