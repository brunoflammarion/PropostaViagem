using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.Dto;
using SistemaUsuarios.Services;
using System.Text.Json;

namespace SistemaUsuarios.Controllers
{
    public class ImportacaoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ImportacaoIAService _ia;
        private readonly ImportacaoPersistenciaService _persistencia;

        public ImportacaoController(
            ApplicationDbContext context,
            ImportacaoIAService ia,
            ImportacaoPersistenciaService persistencia)
        {
            _context = context;
            _ia = ia;
            _persistencia = persistencia;
        }

        private bool UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioId") != null;

        private Guid ObterUsuarioId() =>
            Guid.Parse(HttpContext.Session.GetString("UsuarioId")!);

        private bool SessaoIsMaster() =>
            HttpContext.Session.GetString("TipoUsuario") != "Associado";

        private Guid ObterMasterUsuarioId()
        {
            if (SessaoIsMaster()) return ObterUsuarioId();
            var s = HttpContext.Session.GetString("UsuarioMasterId");
            return string.IsNullOrEmpty(s) ? ObterUsuarioId() : Guid.Parse(s);
        }

        // ── Endpoint principal: um passo só ─────────────────────────────────────
        // POST /Importacao/IniciarComArquivos
        // Cria rascunho + analisa arquivos + salva sessão em banco. Retorna { propostaId, sessaoId, draft, temItens }.
        [HttpPost]
        public async Task<IActionResult> IniciarComArquivos([FromForm] List<IFormFile> arquivos)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (arquivos == null || !arquivos.Any())
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            var usuarioId    = ObterUsuarioId();
            var masterUsuId  = ObterMasterUsuarioId();
            var isMaster     = SessaoIsMaster();

            // 1. Criar proposta rascunho
            var proposta = new Proposta
            {
                Titulo               = "Nova Proposta",
                StatusProposta       = StatusProposta.Rascunho,
                UsuarioId            = usuarioId,
                UsuarioMasterId      = masterUsuId,
                UsuarioResponsavelId = usuarioId,
                DataCriacao          = DateTime.UtcNow,
            };
            _context.Propostas.Add(proposta);
            await _context.SaveChangesAsync();

            // 2. Analisar arquivos com IA
            var (draft, erro) = await _ia.AnalisarAsync(arquivos);

            if (draft == null)
            {
                // Remove a proposta rascunho criada antes de retornar erro
                _context.Propostas.Remove(proposta);
                await _context.SaveChangesAsync();
                return BadRequest(new { erro = erro ?? "Erro ao processar os documentos." });
            }

            // Gera mensagemInicial de fallback se a IA não gerou
            if (string.IsNullOrWhiteSpace(draft.MensagemInicial))
                draft.MensagemInicial = ConstruirMensagemInicial(draft);

            // 3. Salvar sessão de importação no banco
            var sessao = new ImportacaoSessao
            {
                UsuarioId      = usuarioId,
                UsuarioMasterId = masterUsuId,
                PropostaId     = proposta.Id,
                DraftJson      = JsonSerializer.Serialize(draft),
                SourceFiles    = string.Join("|", arquivos.Select(f => f.FileName)),
                Status         = ImportacaoSessaoStatus.AguardandoConfirmacao,
            };
            _context.ImportacaoSessoes.Add(sessao);
            await _context.SaveChangesAsync();

            var temItens = draft.Passageiros.Any() || draft.Voos.Any() ||
                           draft.Destinos.Any()    || draft.Seguros.Any();

            return Json(new { propostaId = proposta.Id, sessaoId = sessao.Id, draft, temItens });
        }

        // GET /Importacao/SessaoDraft/{sessaoId}
        // Carrega o draft persistido no servidor (substitui sessionStorage).
        [HttpGet("/Importacao/SessaoDraft/{sessaoId:guid}")]
        public async Task<IActionResult> SessaoDraft(Guid sessaoId)
        {
            if (!UsuarioLogado()) return Unauthorized(new { erro = "Não autenticado." });

            var uid      = ObterUsuarioId();
            var isMaster = SessaoIsMaster();
            var masterId = ObterMasterUsuarioId();

            var sessao = await _context.ImportacaoSessoes
                .FirstOrDefaultAsync(s => s.Id == sessaoId
                    && s.ExpiradoEm > DateTime.UtcNow
                    && s.Status == ImportacaoSessaoStatus.AguardandoConfirmacao
                    && (isMaster ? s.UsuarioMasterId == masterId : s.UsuarioId == uid));

            if (sessao == null)
                return NotFound(new { erro = "Sessão não encontrada ou expirada." });

            TravelProposalDraft? draft = null;
            try { draft = JsonSerializer.Deserialize<TravelProposalDraft>(sessao.DraftJson); } catch { }

            var temItens = draft != null && (draft.Passageiros.Any() || draft.Voos.Any() ||
                                             draft.Destinos.Any()    || draft.Seguros.Any());

            return Json(new { draft, temItens, sessaoId = sessao.Id });
        }

        // POST /Importacao/MesclarArquivos/{sessaoId}
        // Analisa novos arquivos e mescla no draft existente sem duplicar itens.
        [HttpPost("/Importacao/MesclarArquivos/{sessaoId:guid}")]
        public async Task<IActionResult> MesclarArquivos(Guid sessaoId, [FromForm] List<IFormFile> arquivos)
        {
            if (!UsuarioLogado()) return Unauthorized(new { erro = "Não autenticado." });

            if (arquivos == null || !arquivos.Any())
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            var uid      = ObterUsuarioId();
            var isMaster = SessaoIsMaster();
            var masterId = ObterMasterUsuarioId();

            var sessao = await _context.ImportacaoSessoes
                .FirstOrDefaultAsync(s => s.Id == sessaoId
                    && s.ExpiradoEm > DateTime.UtcNow
                    && s.Status == ImportacaoSessaoStatus.AguardandoConfirmacao
                    && (isMaster ? s.UsuarioMasterId == masterId : s.UsuarioId == uid));

            if (sessao == null)
                return NotFound(new { erro = "Sessão não encontrada ou expirada." });

            // Deserializar draft existente
            TravelProposalDraft existente;
            try { existente = JsonSerializer.Deserialize<TravelProposalDraft>(sessao.DraftJson) ?? new(); }
            catch   { existente = new(); }

            // Analisar novos arquivos
            var (novoDraft, erro) = await _ia.AnalisarAsync(arquivos);
            if (novoDraft == null)
                return BadRequest(new { erro = erro ?? "Erro ao processar os documentos." });

            // Mesclar — retorna o delta (somente itens realmente adicionados)
            var (merged, delta) = MergeDrafts(existente, novoDraft);

            // Salvar draft mesclado
            sessao.DraftJson   = JsonSerializer.Serialize(merged);
            sessao.AtualizadoEm = DateTime.UtcNow;
            var novosArquivos   = string.Join("|", arquivos.Select(f => f.FileName));
            sessao.SourceFiles  = string.IsNullOrEmpty(sessao.SourceFiles)
                ? novosArquivos
                : sessao.SourceFiles + "|" + novosArquivos;
            await _context.SaveChangesAsync();

            // Montar mensagem sobre o que foi adicionado
            var mensagem = ConstruirMensagemMesclagem(delta, arquivos);

            var temDelta = delta.Passageiros.Any() || delta.Voos.Any() ||
                           delta.Destinos.Any()    || delta.Seguros.Any();

            return Json(new { draft = merged, delta, temDelta, mensagem });
        }

        // ── Endpoints legados mantidos ────────────────────────────────────────────

        // POST: /Importacao/CriarRascunho  (mantido para compatibilidade)
        [HttpPost]
        public async Task<IActionResult> CriarRascunho()
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            var usuarioId   = ObterUsuarioId();
            var masterUsuId = ObterMasterUsuarioId();

            var proposta = new Proposta
            {
                Titulo               = "Nova Proposta",
                StatusProposta       = StatusProposta.Rascunho,
                UsuarioId            = usuarioId,
                UsuarioMasterId      = masterUsuId,
                UsuarioResponsavelId = usuarioId,
                DataCriacao          = DateTime.UtcNow,
            };
            _context.Propostas.Add(proposta);
            await _context.SaveChangesAsync();

            return Json(new { propostaId = proposta.Id });
        }

        // GET: /Importacao/Iniciar/{propostaId}
        public async Task<IActionResult> Iniciar(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioId();
            var isMaster  = SessaoIsMaster();

            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            ViewBag.Proposta = proposta;
            return View();
        }

        // POST: /Importacao/AnalisarDocumentos
        [HttpPost]
        public async Task<IActionResult> AnalisarDocumentos(Guid propostaId, List<IFormFile> arquivos)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (arquivos == null || !arquivos.Any())
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            var (draft, erro) = await _ia.AnalisarAsync(arquivos);

            if (erro != null)
                return StatusCode(500, new { erro });

            return Json(draft?.ToPreview());
        }

        // POST: /Importacao/ConfirmarBloco — confirmação incremental bloco a bloco
        [HttpPost]
        public async Task<IActionResult> ConfirmarBloco([FromBody] ConfirmarBlocoRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (request == null || string.IsNullOrWhiteSpace(request.Bloco))
                return BadRequest(new { erro = "Bloco não especificado." });

            var usuarioId = ObterUsuarioId();
            var isMaster  = SessaoIsMaster();

            var resultado = await _persistencia.ImportarBlocoAsync(
                request.PropostaId, usuarioId, isMaster, request);

            if (!resultado.Ok)
                return StatusCode(500, new { erro = resultado.Erro });

            return Json(new { ok = true, bloco = resultado.Bloco, itens = resultado.Itens, mensagem = resultado.Mensagem });
        }

        // POST: /Importacao/Confirmar
        [HttpPost]
        public async Task<IActionResult> Confirmar([FromBody] ConfirmarImportacaoRequest request)
        {
            if (!UsuarioLogado())
                return Unauthorized(new { erro = "Não autenticado." });

            if (request?.Preview == null)
                return BadRequest(new { erro = "Dados inválidos." });

            var usuarioId = ObterUsuarioId();
            var isMaster  = SessaoIsMaster();

            var resultado = await _persistencia.ImportarAsync(
                request.PropostaId, usuarioId, isMaster, request.Preview);

            if (!resultado.Ok)
                return StatusCode(500, new { erro = resultado.Erro });

            return Json(new
            {
                ok = true,
                propostaId = request.PropostaId,
                resumo = new
                {
                    resultado.Passageiros,
                    resultado.Voos,
                    resultado.Destinos,
                    resultado.Hospedagens,
                    resultado.Experiencias,
                    resultado.Transportes,
                    resultado.Seguros
                }
            });
        }

        // ── Helpers internos ──────────────────────────────────────────────────────

        private static string ConstruirMensagemInicial(TravelProposalDraft draft)
        {
            var partes = new List<string>();
            if (draft.Passageiros.Count > 0)
                partes.Add($"{draft.Passageiros.Count} passageiro{(draft.Passageiros.Count != 1 ? "s" : "")}");
            if (draft.Voos.Count > 0)
                partes.Add($"{draft.Voos.Count} voo{(draft.Voos.Count != 1 ? "s" : "")}");
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
            if (draft.Seguros.Count > 0)
                partes.Add($"{draft.Seguros.Count} seguro{(draft.Seguros.Count != 1 ? "s" : "")}");

            return partes.Any()
                ? $"Analisei o documento e encontrei: {string.Join(", ", partes)}.\n\nPosso adicionar esses itens à sua proposta?"
                : "Analisei o documento mas não encontrei itens estruturados. Tente com um documento diferente.";
        }

        private static string ConstruirMensagemMesclagem(TravelProposalDraft delta, List<IFormFile> arquivos)
        {
            var nomes = string.Join(", ", arquivos.Select(f => f.FileName));
            var partes = new List<string>();
            if (delta.Passageiros.Count > 0)
                partes.Add($"{delta.Passageiros.Count} passageiro{(delta.Passageiros.Count != 1 ? "s" : "")}");
            if (delta.Voos.Count > 0)
                partes.Add($"{delta.Voos.Count} voo{(delta.Voos.Count != 1 ? "s" : "")}");
            if (delta.Destinos.Count > 0)
            {
                var h = delta.Destinos.Sum(d => d.Hospedagens.Count);
                var e = delta.Destinos.Sum(d => d.Experiencias.Count);
                var t = delta.Destinos.Sum(d => d.Transportes.Count);
                if (delta.Destinos.Any(d => d.Hospedagens.Count == 0 && d.Experiencias.Count == 0 && d.Transportes.Count == 0))
                    partes.Add($"{delta.Destinos.Count} destino{(delta.Destinos.Count != 1 ? "s" : "")}");
                if (h > 0) partes.Add($"{h} hospedagem{(h != 1 ? "ns" : "")}");
                if (e > 0) partes.Add($"{e} experiência{(e != 1 ? "s" : "")}");
                if (t > 0) partes.Add($"{t} transporte{(t != 1 ? "s" : "")}");
            }
            if (delta.Seguros.Count > 0)
                partes.Add($"{delta.Seguros.Count} seguro{(delta.Seguros.Count != 1 ? "s" : "")}");

            if (!partes.Any())
                return $"Analisei **{nomes}**, mas os itens encontrados já estavam no rascunho. Nada foi duplicado.";

            return $"Analisei **{nomes}** e adicionei ao rascunho: {string.Join(", ", partes)}.";
        }

        // Mescla dois drafts. Retorna (draftMesclado, delta).
        // Delta contém apenas os itens que foram de fato adicionados (não duplicatas).
        private static (TravelProposalDraft merged, TravelProposalDraft delta) MergeDrafts(
            TravelProposalDraft existente, TravelProposalDraft novo)
        {
            var delta = new TravelProposalDraft();

            // Proposta: preenche campos vazios do existente com dados do novo
            var propExist = existente.Proposta ?? new();
            var propNovo  = novo.Proposta ?? new();
            existente.Proposta = new PropostaImportDto
            {
                Titulo           = string.IsNullOrWhiteSpace(propExist.Titulo)           ? propNovo.Titulo           : propExist.Titulo,
                ObservacoesGerais = string.IsNullOrWhiteSpace(propExist.ObservacoesGerais) ? propNovo.ObservacoesGerais : propExist.ObservacoesGerais,
                Operadora        = string.IsNullOrWhiteSpace(propExist.Operadora)        ? propNovo.Operadora        : propExist.Operadora,
                Incluir          = true,
            };

            // Passageiros: adiciona apenas se nome não existir
            var nomesExist = existente.Passageiros
                .Select(p => p.Nome.ToLowerInvariant().Trim())
                .ToHashSet();
            foreach (var p in novo.Passageiros)
            {
                var key = p.Nome.ToLowerInvariant().Trim();
                if (!nomesExist.Contains(key))
                {
                    existente.Passageiros.Add(p);
                    delta.Passageiros.Add(p);
                    nomesExist.Add(key);
                }
            }

            // Voos: adiciona se número do voo não existir
            var voosExist = existente.Voos
                .Select(v => v.NumeroVoo.ToUpperInvariant().Trim())
                .ToHashSet();
            foreach (var v in novo.Voos)
            {
                var key = v.NumeroVoo.ToUpperInvariant().Trim();
                if (!voosExist.Contains(key))
                {
                    existente.Voos.Add(v);
                    delta.Voos.Add(v);
                    voosExist.Add(key);
                }
            }

            // Destinos: mescla por nome
            var destinosExistMap = existente.Destinos
                .ToDictionary(d => d.Nome.ToLowerInvariant().Trim());

            foreach (var dNovo in novo.Destinos)
            {
                var key = dNovo.Nome.ToLowerInvariant().Trim();
                if (!destinosExistMap.TryGetValue(key, out var dExist))
                {
                    // Destino inteiramente novo
                    existente.Destinos.Add(dNovo);
                    delta.Destinos.Add(dNovo);
                    destinosExistMap[key] = dNovo;
                }
                else
                {
                    // Destino já existe — mescla os sub-itens
                    var deltaDestino = new DestinoImportDto { Nome = dExist.Nome, Incluir = true };

                    var hospNomes = dExist.Hospedagens
                        .Select(h => h.Nome.ToLowerInvariant().Trim()).ToHashSet();
                    foreach (var h in dNovo.Hospedagens)
                    {
                        var hk = h.Nome.ToLowerInvariant().Trim();
                        if (!hospNomes.Contains(hk))
                        {
                            dExist.Hospedagens.Add(h);
                            deltaDestino.Hospedagens.Add(h);
                            hospNomes.Add(hk);
                        }
                    }

                    var expNomes = dExist.Experiencias
                        .Select(e => e.TipoPasseio.ToLowerInvariant().Trim()).ToHashSet();
                    foreach (var e in dNovo.Experiencias)
                    {
                        var ek = e.TipoPasseio.ToLowerInvariant().Trim();
                        if (!expNomes.Contains(ek))
                        {
                            dExist.Experiencias.Add(e);
                            deltaDestino.Experiencias.Add(e);
                            expNomes.Add(ek);
                        }
                    }

                    var transpTitulos = dExist.Transportes
                        .Select(t => t.Titulo.ToLowerInvariant().Trim()).ToHashSet();
                    foreach (var t in dNovo.Transportes)
                    {
                        var tk = t.Titulo.ToLowerInvariant().Trim();
                        if (!transpTitulos.Contains(tk))
                        {
                            dExist.Transportes.Add(t);
                            deltaDestino.Transportes.Add(t);
                            transpTitulos.Add(tk);
                        }
                    }

                    if (deltaDestino.Hospedagens.Any() || deltaDestino.Experiencias.Any() || deltaDestino.Transportes.Any())
                        delta.Destinos.Add(deltaDestino);
                }
            }

            // Seguros: adiciona se título não existir
            var segTitulos = existente.Seguros
                .Select(s => s.Titulo.ToLowerInvariant().Trim()).ToHashSet();
            foreach (var s in novo.Seguros)
            {
                var sk = s.Titulo.ToLowerInvariant().Trim();
                if (!segTitulos.Contains(sk))
                {
                    existente.Seguros.Add(s);
                    delta.Seguros.Add(s);
                    segTitulos.Add(sk);
                }
            }

            // Valores financeiros: prefere o novo se mais completo
            if (novo.ValoresFinanceiros != null)
            {
                existente.ValoresFinanceiros ??= novo.ValoresFinanceiros;
                if (novo.ValoresFinanceiros.ValorTotal.HasValue && existente.ValoresFinanceiros.ValorTotal == null)
                    existente.ValoresFinanceiros.ValorTotal = novo.ValoresFinanceiros.ValorTotal;
            }

            // Alertas e pendentes: union sem duplicatas
            existente.Alertas  = existente.Alertas.Union(novo.Alertas).Distinct().ToList();
            existente.Pendentes = existente.Pendentes.Union(novo.Pendentes).Distinct().ToList();

            return (existente, delta);
        }
    }
}
