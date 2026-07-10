using SistemaUsuarios.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.ViewModels;

namespace SistemaUsuarios.Controllers
{
    public class ClienteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BlobStorageService _blob;

        public ClienteController(ApplicationDbContext context, BlobStorageService blob)
        {
            _context = context;
            _blob = blob;
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

        // ─── MÓDULO DE GESTÃO DE CLIENTES ────────────────────────────────────────

        // GET: /Cliente
        [HttpGet]
        public async Task<IActionResult> Index(string? termoBusca)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var hoje = DateTime.Today;

            // Carrega todos os clientes com suas propostas e destinos
            var clientesQuery = _context.Clientes
                .Where(c => c.UsuarioId == usuarioId && !c.IsDeleted)
                .Include(c => c.Propostas)
                    .ThenInclude(p => p.Destinos)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(termoBusca))
            {
                var t = termoBusca.Trim().ToLower();
                clientesQuery = clientesQuery.Where(c =>
                    c.Nome.ToLower().Contains(t) ||
                    (c.Email != null && c.Email.ToLower().Contains(t)) ||
                    (c.Telefone != null && c.Telefone.Contains(t)));
            }

            var clientes = await clientesQuery.OrderBy(c => c.Nome).ToListAsync();

            // Métricas globais (sem filtro, respeitando hierarquia)
            var isMaster = SessaoIsMaster();
            var totalPropostas = await _context.Propostas.CountAsync(p =>
                isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId);
            var aprovadas = await _context.Propostas.CountAsync(p =>
                (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId)
                && p.StatusProposta == StatusProposta.Aprovada);

            // Monta lista de resumo
            var resumos = clientes.Select(c =>
            {
                var emViagem = c.Propostas.Any(p =>
                    p.DataInicio.HasValue && p.DataFim.HasValue &&
                    p.DataInicio.Value.Date <= hoje && p.DataFim.Value.Date >= hoje &&
                    (p.StatusProposta == StatusProposta.Aprovada || p.StatusProposta == StatusProposta.Enviada));

                var ultimosDestinos = c.Propostas
                    .OrderByDescending(p => p.DataCriacao)
                    .Take(3)
                    .SelectMany(p => p.Destinos.Select(d => d.Nome))
                    .Distinct()
                    .Take(3)
                    .ToList();

                return new ClienteResumoItem
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    FotoPath = c.FotoPath,
                    Email = c.Email,
                    Telefone = c.Telefone,
                    DataNascimento = c.DataNascimento,
                    Idade = c.DataNascimento.HasValue
                        ? (int?)((hoje - c.DataNascimento.Value).Days / 365)
                        : null,
                    TotalPropostas = c.Propostas.Count,
                    PropostasAprovadas = c.Propostas.Count(p => p.StatusProposta == StatusProposta.Aprovada),
                    EmViagem = emViagem,
                    DataCriacao = c.DataCriacao,
                    UltimosDestinos = ultimosDestinos
                };
            }).ToList();

            // Aniversários próximos (30 dias) — sobre todos os clientes sem filtro
            var todosOsClientes = string.IsNullOrWhiteSpace(termoBusca) ? clientes :
                await _context.Clientes.Where(c => c.UsuarioId == usuarioId && !c.IsDeleted).ToListAsync();

            var aniversarios = todosOsClientes
                .Where(c => c.DataNascimento.HasValue)
                .Select(c =>
                {
                    var dn = c.DataNascimento!.Value;
                    var prox = new DateTime(hoje.Year, dn.Month, dn.Day);
                    if (prox < hoje) prox = prox.AddYears(1);
                    var dias = (prox - hoje).Days;
                    return new AniversarioItem
                    {
                        ClienteId = c.Id,
                        Nome = c.Nome,
                        FotoPath = c.FotoPath,
                        DataNascimento = dn,
                        DiasAteAniversario = dias,
                        IdadeQueVaiFazer = prox.Year - dn.Year
                    };
                })
                .Where(a => a.DiasAteAniversario <= 30)
                .OrderBy(a => a.DiasAteAniversario)
                .ToList();

            var vm = new ClienteListViewModel
            {
                TotalClientes = await _context.Clientes.CountAsync(c => c.UsuarioId == usuarioId && !c.IsDeleted),
                TotalPropostas = totalPropostas,
                PropostasAprovadas = aprovadas,
                ClientesEmViagem = resumos.Count(r => r.EmViagem),
                AniversariosProximos = aniversarios,
                TermoBusca = termoBusca,
                Clientes = resumos
            };

            return View(vm);
        }

        // GET: /Cliente/Detalhe/{id}
        [HttpGet]
        public async Task<IActionResult> Detalhe(Guid id, string? tab)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();
            var hoje = DateTime.Today;

            var cliente = await _context.Clientes
                .Include(c => c.Propostas)
                    .ThenInclude(p => p.Destinos)
                .Include(c => c.Propostas)
                    .ThenInclude(p => p.PassageirosProposta)
                .Include(c => c.ClienteIndicador)
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

            if (cliente == null) return NotFound();

            // Propostas
            var propostaItems = cliente.Propostas
                .OrderByDescending(p => p.DataCriacao)
                .Select(p => new PropostaClienteItem
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    StatusProposta = p.StatusProposta,
                    DataInicio = p.DataInicio,
                    DataFim = p.DataFim,
                    NumeroPassageiros = p.NumeroPassageiros,
                    NumeroCriancas = p.NumeroCriancas,
                    FotoCapa = p.FotoCapa,
                    LinkPublicoAtivo = p.LinkPublicoAtivo,
                    DataCriacao = p.DataCriacao,
                    Destinos = p.Destinos.OrderBy(d => d.Ordem).Select(d => d.Nome).ToList()
                }).ToList();

            // Passageiros recorrentes (agrupa por nome normalizado)
            var passageirosRecorrentes = cliente.Propostas
                .SelectMany(p => p.PassageirosProposta)
                .GroupBy(pp => pp.Nome.Trim().ToLower())
                .Select(g =>
                {
                    var primeiro = g.OrderByDescending(pp => pp.DataCriacao).First();
                    return new PassageiroRecorrenteItem
                    {
                        Nome = primeiro.Nome,
                        Ocorrencias = g.Count(),
                        Relacionamento = primeiro.Relacionamento,
                        DataNascimento = primeiro.DataNascimento
                    };
                })
                .OrderByDescending(p => p.Ocorrencias)
                .ThenBy(p => p.Nome)
                .ToList();

            // Agenda (próximos 365 dias)
            var agenda = new List<EventoAgendaItem>();

            // Aniversário do cliente
            if (cliente.DataNascimento.HasValue)
            {
                var dn = cliente.DataNascimento.Value;
                var proxAniversario = new DateTime(hoje.Year, dn.Month, dn.Day);
                if (proxAniversario < hoje) proxAniversario = proxAniversario.AddYears(1);
                var dias = (proxAniversario - hoje).Days;
                if (dias <= 365)
                {
                    agenda.Add(new EventoAgendaItem
                    {
                        Tipo = "aniversario",
                        Titulo = $"Aniversário de {cliente.Nome.Split(' ')[0]}",
                        Subtitulo = $"Fará {proxAniversario.Year - dn.Year} anos",
                        Data = proxAniversario,
                        Icone = "fas fa-birthday-cake",
                        CorCss = "warning"
                    });
                }
            }

            // Viagens das propostas
            foreach (var p in cliente.Propostas.Where(p => p.DataInicio.HasValue && p.DataFim.HasValue))
            {
                var destNomes = p.Destinos.OrderBy(d => d.Ordem)
                    .Select(d => d.Nome).Take(2).ToList();
                var resumoDestinos = destNomes.Any() ? string.Join(", ", destNomes) : "Sem destino";

                if (p.DataInicio!.Value.Date <= hoje && p.DataFim!.Value.Date >= hoje)
                {
                    agenda.Add(new EventoAgendaItem
                    {
                        Tipo = "viagem_andamento",
                        Titulo = p.Titulo,
                        Subtitulo = $"Em andamento · {resumoDestinos}",
                        Data = p.DataFim.Value,
                        Icone = "fas fa-plane",
                        CorCss = "success",
                        PropostaId = p.Id
                    });
                }
                else if (p.DataInicio.Value.Date > hoje && p.DataInicio.Value.Date <= hoje.AddDays(365))
                {
                    var diasPara = (p.DataInicio.Value.Date - hoje).Days;
                    agenda.Add(new EventoAgendaItem
                    {
                        Tipo = "viagem_proxima",
                        Titulo = p.Titulo,
                        Subtitulo = $"Saída em {diasPara} dia(s) · {resumoDestinos}",
                        Data = p.DataInicio.Value,
                        Icone = "fas fa-suitcase-rolling",
                        CorCss = "primary",
                        PropostaId = p.Id
                    });
                }
            }

            agenda = agenda.OrderBy(e => e.Data).ToList();

            var vm = new ClienteDetalheViewModel
            {
                Id = cliente.Id,
                Nome = cliente.Nome,
                FotoPath = cliente.FotoPath,
                Email = cliente.Email,
                Telefone = cliente.Telefone,
                DataNascimento = cliente.DataNascimento,
                Genero = cliente.Genero,
                Cpf = cliente.Cpf,
                DataCriacao = cliente.DataCriacao,
                DataEntradaCliente = cliente.DataEntradaCliente,
                Cep = cliente.Cep,
                Logradouro = cliente.Logradouro,
                Cidade = cliente.Cidade,
                Estado = cliente.Estado,
                ClienteIndicadorId = cliente.ClienteIndicadorId,
                ClienteIndicadorNome = cliente.ClienteIndicador?.Nome,
                IsDeleted = cliente.IsDeleted,
                DeletedAt = cliente.DeletedAt,
                TotalPropostas = propostaItems.Count,
                PropostasAprovadas = propostaItems.Count(p => p.StatusProposta == StatusProposta.Aprovada),
                PropostasEnviadas = propostaItems.Count(p => p.StatusProposta == StatusProposta.Enviada),
                EmViagem = propostaItems.Any(p => p.EmAndamento),
                Propostas = propostaItems,
                PassageirosRecorrentes = passageirosRecorrentes,
                Agenda = agenda,
                ActiveTab = tab ?? (TempData["ActiveTab"] as string) ?? "resumo"
            };

            return View(vm);
        }

        // GET: /Cliente/Criar
        [HttpGet]
        public IActionResult Criar()
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var vm = new ClienteDetalheViewModel
            {
                DataCriacao = DateTime.Now,
                DataEntradaCliente = DateTime.Today
            };
            return View(vm);
        }

        // POST: /Cliente/Criar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(
            string nome,
            string? email,
            string? telefone,
            DateTime? dataNascimento,
            Genero? genero,
            string? cpf,
            DateTime? dataEntradaCliente,
            string? cep,
            string? logradouro,
            string? cidade,
            string? estado,
            Guid? clienteIndicadorId,
            IFormFile? foto)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do cliente é obrigatório.";
                return View(new ClienteDetalheViewModel
                {
                    DataCriacao = DateTime.Now,
                    DataEntradaCliente = dataEntradaCliente ?? DateTime.Today,
                    Nome = nome ?? "",
                    Email = email,
                    Telefone = telefone,
                    DataNascimento = dataNascimento,
                    Genero = genero,
                    Cpf = cpf,
                    Cep = cep,
                    Logradouro = logradouro,
                    Cidade = cidade,
                    Estado = estado,
                    ClienteIndicadorId = clienteIndicadorId
                });
            }

            if (clienteIndicadorId.HasValue)
            {
                var indicadorExiste = await _context.Clientes.AnyAsync(c =>
                    c.Id == clienteIndicadorId.Value && c.UsuarioId == usuarioId && !c.IsDeleted);
                if (!indicadorExiste)
                {
                    TempData["Erro"] = "Cliente indicador não encontrado.";
                    return View(new ClienteDetalheViewModel { DataCriacao = DateTime.Now, DataEntradaCliente = DateTime.Today });
                }
            }

            string? fotoPath = null;
            if (foto != null && foto.Length > 0)
            {
                try { fotoPath = await SalvarFotoAsync(foto); }
                catch (InvalidOperationException ex)
                {
                    TempData["Erro"] = ex.Message;
                    return View(new ClienteDetalheViewModel { DataCriacao = DateTime.Now, DataEntradaCliente = DateTime.Today });
                }
            }

            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Nome = nome.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
                DataNascimento = dataNascimento,
                Genero = genero,
                Cpf = string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim(),
                FotoPath = fotoPath,
                DataCriacao = DateTime.Now,
                DataEntradaCliente = dataEntradaCliente ?? DateTime.Today,
                Cep = string.IsNullOrWhiteSpace(cep) ? null : cep.Trim(),
                Logradouro = string.IsNullOrWhiteSpace(logradouro) ? null : logradouro.Trim(),
                Cidade = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim(),
                Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpper(),
                ClienteIndicadorId = clienteIndicadorId
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Cliente criado com sucesso.";
            return RedirectToAction("Detalhe", new { id = cliente.Id });
        }

        // POST: /Cliente/EditarDados
        // Salva dados básicos do cliente a partir da tela de detalhe (sem propostaId).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarDados(
            Guid id,
            string nome,
            string? email,
            string? telefone,
            DateTime? dataNascimento,
            Genero? genero,
            string? cpf,
            DateTime? dataEntradaCliente,
            string? cep,
            string? logradouro,
            string? cidade,
            string? estado,
            Guid? clienteIndicadorId,
            IFormFile? foto)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId && !c.IsDeleted);

            if (cliente == null) return NotFound();

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do cliente é obrigatório.";
                return RedirectToAction("Detalhe", new { id });
            }

            // Valida indicador: mesmo usuário, não é ele mesmo, não está excluído
            if (clienteIndicadorId.HasValue)
            {
                if (clienteIndicadorId.Value == id)
                {
                    TempData["Erro"] = "Um cliente não pode ser indicador de si mesmo.";
                    return RedirectToAction("Detalhe", new { id });
                }
                var indicadorExiste = await _context.Clientes.AnyAsync(c =>
                    c.Id == clienteIndicadorId.Value && c.UsuarioId == usuarioId && !c.IsDeleted);
                if (!indicadorExiste)
                {
                    TempData["Erro"] = "Cliente indicador não encontrado.";
                    return RedirectToAction("Detalhe", new { id });
                }
            }

            if (foto != null && foto.Length > 0)
            {
                try
                {
                    var novaFoto = await SalvarFotoAsync(foto);
                    DeletarArquivoFisico(cliente.FotoPath);
                    cliente.FotoPath = novaFoto;
                }
                catch (InvalidOperationException ex)
                {
                    TempData["Erro"] = ex.Message;
                    return RedirectToAction("Detalhe", new { id });
                }
            }

            cliente.Nome = nome.Trim();
            cliente.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            cliente.Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
            cliente.DataNascimento = dataNascimento;
            cliente.Genero = genero;
            cliente.Cpf = string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim();
            cliente.DataEntradaCliente = dataEntradaCliente;
            cliente.Cep = string.IsNullOrWhiteSpace(cep) ? null : cep.Trim();
            cliente.Logradouro = string.IsNullOrWhiteSpace(logradouro) ? null : logradouro.Trim();
            cliente.Cidade = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim();
            cliente.Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpper();
            cliente.ClienteIndicadorId = clienteIndicadorId;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Dados atualizados com sucesso!";
            TempData["ActiveTab"] = "resumo";
            return RedirectToAction("Detalhe", new { id });
        }

        // POST: /Cliente/ExcluirLogico
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirLogico(Guid id)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId && !c.IsDeleted);

            if (cliente == null) return NotFound();

            cliente.IsDeleted = true;
            cliente.DeletedAt = DateTime.Now;
            cliente.DeletedByUserId = usuarioId;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Cliente \"{cliente.Nome}\" foi arquivado.";
            return RedirectToAction("Index");
        }

        // GET: /Cliente/Buscar?termo=xxx
        // Retorna JSON com clientes do usuário logado que correspondem ao termo.
        [HttpGet]
        public async Task<IActionResult> Buscar(string? termo)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var query = _context.Clientes.Where(c => c.UsuarioId == usuarioId && !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(termo))
            {
                var t = termo.Trim().ToLower();
                query = query.Where(c =>
                    c.Nome.ToLower().Contains(t) ||
                    (c.Email != null && c.Email.ToLower().Contains(t)) ||
                    (c.Cpf != null && c.Cpf.Contains(t)));
            }

            var clientes = await query
                .OrderBy(c => c.Nome)
                .Take(10)
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Email,
                    c.Telefone,
                    c.FotoPath,
                    IdadeCalculada = c.DataNascimento.HasValue
                        ? (int?)((DateTime.Today - c.DataNascimento.Value).Days / 365)
                        : null
                })
                .ToListAsync();

            return Json(clientes);
        }

        // GET: /Cliente/BuscarIndicador?termo=xxx&excluirId=yyy
        // Para autocomplete de indicação — exclui o próprio cliente e deletados.
        [HttpGet]
        public async Task<IActionResult> BuscarIndicador(string? termo, Guid? excluirId)
        {
            if (!UsuarioLogado())
                return Unauthorized();

            var usuarioId = ObterUsuarioLogadoId();

            var query = _context.Clientes
                .Where(c => c.UsuarioId == usuarioId && !c.IsDeleted);

            if (excluirId.HasValue)
                query = query.Where(c => c.Id != excluirId.Value);

            if (!string.IsNullOrWhiteSpace(termo))
            {
                var t = termo.Trim().ToLower();
                query = query.Where(c => c.Nome.ToLower().Contains(t) ||
                    (c.Email != null && c.Email.ToLower().Contains(t)));
            }

            var clientes = await query
                .OrderBy(c => c.Nome)
                .Take(8)
                .Select(c => new { c.Id, c.Nome, c.Email })
                .ToListAsync();

            return Json(clientes);
        }

        // POST: /Cliente/CriarEAssociar
        // Cria um novo cliente e já o associa à proposta informada.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarEAssociar(
            Guid propostaId,
            string nome,
            string? email,
            string? telefone,
            DateTime? dataNascimento,
            Genero? genero,
            string? cpf,
            IFormFile? foto)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do cliente é obrigatório.";
                return RedirectToPassageiros(propostaId);
            }

            string? fotoPath = null;
            if (foto != null && foto.Length > 0)
            {
                try { fotoPath = await SalvarFotoAsync(foto); }
                catch (InvalidOperationException ex)
                {
                    TempData["Erro"] = ex.Message;
                    return RedirectToPassageiros(propostaId);
                }
            }

            var cliente = new Cliente
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Nome = nome.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
                DataNascimento = dataNascimento,
                Genero = genero,
                Cpf = string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim(),
                FotoPath = fotoPath,
                DataCriacao = DateTime.Now,
                DataEntradaCliente = DateTime.Now
            };

            _context.Clientes.Add(cliente);

            proposta.ClienteId = cliente.Id;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Cliente \"{cliente.Nome}\" cadastrado e associado à proposta!";
            return RedirectToPassageiros(propostaId);
        }

        // POST: /Cliente/Associar
        // Associa um cliente já existente a uma proposta.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Associar(Guid propostaId, Guid clienteId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.UsuarioId == usuarioId && !c.IsDeleted);

            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToPassageiros(propostaId);
            }

            proposta.ClienteId = clienteId;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Cliente \"{cliente.Nome}\" associado à proposta!";
            return RedirectToPassageiros(propostaId);
        }

        // POST: /Cliente/Dissociar
        // Remove a associação do cliente da proposta (não exclui o cliente).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dissociar(Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var isMaster = SessaoIsMaster();
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
            {
                TempData["Erro"] = "Proposta não encontrada.";
                return RedirectToAction("Index", "Proposta");
            }

            proposta.ClienteId = null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Cliente desassociado da proposta.";
            return RedirectToPassageiros(propostaId);
        }

        // POST: /Cliente/Editar
        // Atualiza os dados do cliente (afeta todas as propostas vinculadas a ele).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            Guid id,
            Guid propostaId,
            string nome,
            string? email,
            string? telefone,
            DateTime? dataNascimento,
            Genero? genero,
            string? cpf,
            IFormFile? foto)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToPassageiros(propostaId);
            }

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Erro"] = "Nome do cliente é obrigatório.";
                return RedirectToPassageiros(propostaId);
            }

            if (foto != null && foto.Length > 0)
            {
                try
                {
                    var novaFoto = await SalvarFotoAsync(foto);
                    DeletarArquivoFisico(cliente.FotoPath);
                    cliente.FotoPath = novaFoto;
                }
                catch (InvalidOperationException ex)
                {
                    TempData["Erro"] = ex.Message;
                    return RedirectToPassageiros(propostaId);
                }
            }

            cliente.Nome = nome.Trim();
            cliente.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            cliente.Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
            cliente.DataNascimento = dataNascimento;
            cliente.Genero = genero;
            cliente.Cpf = string.IsNullOrWhiteSpace(cpf) ? null : cpf.Trim();

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Dados do cliente atualizados!";
            return RedirectToPassageiros(propostaId);
        }

        // POST: /Cliente/RemoverFoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverFoto(Guid clienteId, Guid propostaId)
        {
            if (!UsuarioLogado())
                return RedirectToAction("Login", "Auth");

            var usuarioId = ObterUsuarioLogadoId();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == clienteId && c.UsuarioId == usuarioId);

            if (cliente == null)
            {
                TempData["Erro"] = "Cliente não encontrado.";
                return RedirectToPassageiros(propostaId);
            }

            DeletarArquivoFisico(cliente.FotoPath);
            cliente.FotoPath = null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Foto removida.";
            return RedirectToPassageiros(propostaId);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private Task<string> SalvarFotoAsync(IFormFile foto)
            => _blob.SalvarAsync(foto, "clientes");

        private void DeletarArquivoFisico(string? url)
            => _ = _blob.DeletarAsync(url);
    }
}
