using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Services
{
    // ── DTOs ────────────────────────────────────────────────────────────────────
    public class CriarTarefaDto
    {
        public Guid    UsuarioId      { get; set; }
        public Guid?   ClienteId      { get; set; }
        public Guid?   PropostaId     { get; set; }
        public string  Titulo         { get; set; } = "";
        public string? Descricao      { get; set; }
        public DateTime DataVencimento { get; set; }
        public string? Tipo           { get; set; }
        public string? Prioridade     { get; set; }
    }

    public class EditarTarefaDto
    {
        public Guid    Id             { get; set; }
        public Guid    UsuarioId      { get; set; }
        public string  Titulo         { get; set; } = "";
        public string? Descricao      { get; set; }
        public DateTime DataVencimento { get; set; }
        public string? Tipo           { get; set; }
        public string? Prioridade     { get; set; }
    }

    // ── Interface ────────────────────────────────────────────────────────────────
    public interface ITarefaService
    {
        Task<Tarefa> CriarTarefaManualAsync(CriarTarefaDto dto);
        Task         EditarTarefaAsync(EditarTarefaDto dto);
        Task         ConcluirTarefaAsync(Guid tarefaId, Guid usuarioId);
        Task         CancelarTarefaAsync(Guid tarefaId, Guid usuarioId);

        Task GerarTarefasParaViagemAsync(Guid propostaId);
        Task GerarTarefaFollowUpVisualizacaoAsync(Guid propostaId);
        Task ProcessarAniversariosDoUsuarioAsync(Guid usuarioId);

        // Scheduler-ready — não depende de HttpContext
        Task ProcessarLembretesDoUsuarioAsync(Guid usuarioId);
        Task ProcessarLembretesDoDiaAsync();

        Task<List<Tarefa>> ListarPorUsuarioAsync(Guid usuarioId, DateTime? de = null, DateTime? ate = null, string? status = null);
        Task<List<Tarefa>> ListarHojeAsync(Guid usuarioId);
        Task<List<Tarefa>> ListarAtrasadasAsync(Guid usuarioId);

        Task<List<ConfiguracaoLembrete>> ObterConfiguracoesAsync(Guid usuarioId);
        Task SalvarConfiguracoesAsync(Guid usuarioId, List<(string TemplateCodigo, bool Habilitado)> configs);
    }

    // ── Serviço ──────────────────────────────────────────────────────────────────
    public class TarefaService : ITarefaService
    {
        // Template codes — constantes públicas para uso nos controllers
        public const string FOLLOWUP_VISUALIZACAO = "FOLLOWUP_PROPOSTA_VISUALIZADA";
        public const string OFERECER_SEGURO       = "OFERECER_SEGURO";
        public const string REVISAR_VOUCHERS      = "REVISAR_VOUCHERS";
        public const string CHECKIN_VOO           = "CHECKIN_VOO";
        public const string CONFIRMAR_HOTEL       = "CONFIRMAR_HOTEL";
        public const string BOA_VIAGEM            = "BOA_VIAGEM";
        public const string PEDIR_FEEDBACK        = "PEDIR_FEEDBACK";
        public const string PEDIR_INDICACAO       = "PEDIR_INDICACAO";
        public const string ANIVERSARIO_CLIENTE   = "ANIVERSARIO_CLIENTE";

        // Definição de todos os templates com valores padrão
        private static readonly (string Tipo, string Codigo, bool HabPadrao, int OffsetDias, string Momento)[] DefaultTemplates =
        {
            (TarefaTipo.Followup,    FOLLOWUP_VISUALIZACAO, true,   0, MomentoReferenciaLembrete.DiaEvento),
            (TarefaTipo.Followup,    OFERECER_SEGURO,       true, -15, MomentoReferenciaLembrete.AntesInicio),
            (TarefaTipo.Operacional, REVISAR_VOUCHERS,      true,  -7, MomentoReferenciaLembrete.AntesInicio),
            (TarefaTipo.Operacional, CHECKIN_VOO,           true,  -2, MomentoReferenciaLembrete.AntesInicio),
            (TarefaTipo.Operacional, CONFIRMAR_HOTEL,       true,  -3, MomentoReferenciaLembrete.AntesInicio),
            (TarefaTipo.Operacional, BOA_VIAGEM,            true,  -1, MomentoReferenciaLembrete.AntesInicio),
            (TarefaTipo.Comercial,   PEDIR_FEEDBACK,        true,  +2, MomentoReferenciaLembrete.AposFim),
            (TarefaTipo.Comercial,   PEDIR_INDICACAO,       true,  +7, MomentoReferenciaLembrete.AposFim),
            (TarefaTipo.Aniversario, ANIVERSARIO_CLIENTE,   true,   0, MomentoReferenciaLembrete.DiaEvento),
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<TarefaService> _logger;

        public TarefaService(ApplicationDbContext context, ILogger<TarefaService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // ── CRUD manual ──────────────────────────────────────────────────────────

        public async Task<Tarefa> CriarTarefaManualAsync(CriarTarefaDto dto)
        {
            var tarefa = new Tarefa
            {
                UsuarioId             = dto.UsuarioId,
                ClienteId             = dto.ClienteId,
                PropostaId            = dto.PropostaId,
                Titulo                = dto.Titulo.Trim(),
                Descricao             = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
                DataVencimento        = dto.DataVencimento.Date,
                Tipo                  = dto.Tipo ?? TarefaTipo.Geral,
                Prioridade            = dto.Prioridade ?? TarefaPrioridade.Media,
                Status                = TarefaStatus.Pendente,
                Origem                = TarefaOrigem.Manual,
                GeradaAutomaticamente = false
            };
            _context.Tarefas.Add(tarefa);
            await _context.SaveChangesAsync();
            return tarefa;
        }

        public async Task EditarTarefaAsync(EditarTarefaDto dto)
        {
            var tarefa = await _context.Tarefas
                .FirstOrDefaultAsync(t => t.Id == dto.Id && t.UsuarioId == dto.UsuarioId && !t.IsDeleted);
            if (tarefa == null) return;

            tarefa.Titulo          = dto.Titulo.Trim();
            tarefa.Descricao       = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim();
            tarefa.DataVencimento  = dto.DataVencimento.Date;
            if (dto.Tipo      != null) tarefa.Tipo      = dto.Tipo;
            if (dto.Prioridade != null) tarefa.Prioridade = dto.Prioridade;
            tarefa.DataAtualizacao = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task ConcluirTarefaAsync(Guid tarefaId, Guid usuarioId)
        {
            var tarefa = await _context.Tarefas
                .FirstOrDefaultAsync(t => t.Id == tarefaId && t.UsuarioId == usuarioId && !t.IsDeleted);
            if (tarefa == null) return;

            tarefa.Status          = TarefaStatus.Concluida;
            tarefa.DataConclusao   = DateTime.Now;
            tarefa.DataAtualizacao = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task CancelarTarefaAsync(Guid tarefaId, Guid usuarioId)
        {
            var tarefa = await _context.Tarefas
                .FirstOrDefaultAsync(t => t.Id == tarefaId && t.UsuarioId == usuarioId && !t.IsDeleted);
            if (tarefa == null) return;

            tarefa.Status          = TarefaStatus.Cancelada;
            tarefa.DataAtualizacao = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        // ── Geração automática por evento ────────────────────────────────────────

        public async Task GerarTarefasParaViagemAsync(Guid propostaId)
        {
            var proposta = await _context.Propostas
                .Include(p => p.Voos)
                .Include(p => p.Destinos)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == propostaId);

            if (proposta is null || !proposta.DataInicio.HasValue || !proposta.DataFim.HasValue)
                return;

            var usuarioId  = proposta.UsuarioResponsavelId ?? proposta.UsuarioMasterId ?? proposta.UsuarioId;
            var dataInicio = proposta.DataInicio.Value.Date;
            var dataFim    = proposta.DataFim.Value.Date;
            var configs    = await ObterConfigsDicionario(usuarioId);

            // Templates baseados em DataInicio
            await TentarGerarAsync(configs, OFERECER_SEGURO,  usuarioId, proposta.ClienteId, propostaId, dataInicio);
            await TentarGerarAsync(configs, REVISAR_VOUCHERS, usuarioId, proposta.ClienteId, propostaId, dataInicio);
            await TentarGerarAsync(configs, CONFIRMAR_HOTEL,  usuarioId, proposta.ClienteId, propostaId, dataInicio);
            await TentarGerarAsync(configs, BOA_VIAGEM,       usuarioId, proposta.ClienteId, propostaId, dataInicio);

            // Templates baseados em DataFim
            await TentarGerarAsync(configs, PEDIR_FEEDBACK,   usuarioId, proposta.ClienteId, propostaId, dataFim);
            await TentarGerarAsync(configs, PEDIR_INDICACAO,  usuarioId, proposta.ClienteId, propostaId, dataFim);

            // CHECKIN_VOO: usa data do primeiro voo com horário definido
            if (configs.TryGetValue(CHECKIN_VOO, out var cfgCheckin) && cfgCheckin.Habilitado)
            {
                var primeiroVoo = proposta.Voos
                    .Where(v => v.HorarioSaida.HasValue)
                    .OrderBy(v => v.HorarioSaida)
                    .FirstOrDefault();

                var dataRef = primeiroVoo?.HorarioSaida?.Date ?? dataInicio;
                await TentarGerarAsync(configs, CHECKIN_VOO, usuarioId, proposta.ClienteId, propostaId, dataRef);
            }
        }

        public async Task GerarTarefaFollowUpVisualizacaoAsync(Guid propostaId)
        {
            try
            {
                var proposta = await _context.Propostas.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == propostaId);
                if (proposta is null) return;

                // Não gerar follow-up para propostas já fechadas
                if (proposta.StatusProposta == StatusProposta.Aprovada  ||
                    proposta.StatusProposta == StatusProposta.Cancelada ||
                    proposta.StatusProposta == StatusProposta.Rejeitada)
                    return;

                var usuarioId = proposta.UsuarioResponsavelId ?? proposta.UsuarioMasterId ?? proposta.UsuarioId;
                var configs   = await ObterConfigsDicionario(usuarioId);

                if (!configs.TryGetValue(FOLLOWUP_VISUALIZACAO, out var cfg) || !cfg.Habilitado)
                    return;

                // Idempotência: só cria se não houver followup PENDENTE para esta proposta
                var jaTemPendente = await _context.Tarefas.AnyAsync(t =>
                    t.PropostaId == propostaId
                    && t.TemplateCodigo == FOLLOWUP_VISUALIZACAO
                    && t.Status == TarefaStatus.Pendente
                    && !t.IsDeleted);

                if (!jaTemPendente)
                    await CriarAutomaticaAsync(usuarioId, proposta.ClienteId, propostaId, FOLLOWUP_VISUALIZACAO, DateTime.Today, cfg.Tipo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar follow-up para proposta {PropostaId}", propostaId);
            }
        }

        public async Task ProcessarAniversariosDoUsuarioAsync(Guid usuarioId)
        {
            var configs = await ObterConfigsDicionario(usuarioId);
            if (!configs.TryGetValue(ANIVERSARIO_CLIENTE, out var cfg) || !cfg.Habilitado)
                return;

            var hoje    = DateTime.Today;
            var ate     = hoje.AddDays(30);

            var clientes = await _context.Clientes
                .Where(c => c.UsuarioId == usuarioId && !c.IsDeleted && c.DataNascimento.HasValue)
                .AsNoTracking()
                .ToListAsync();

            foreach (var cliente in clientes)
            {
                var nasc = cliente.DataNascimento!.Value;
                foreach (var ano in new[] { hoje.Year, hoje.Year + 1 })
                {
                    DateTime dataAniv;
                    try { dataAniv = new DateTime(ano, nasc.Month, nasc.Day); }
                    catch { continue; } // 29/fev em ano não bissexto

                    if (dataAniv < hoje || dataAniv > ate) continue;

                    var jaExiste = await _context.Tarefas.AnyAsync(t =>
                        t.UsuarioId == usuarioId
                        && t.ClienteId == cliente.Id
                        && t.TemplateCodigo == ANIVERSARIO_CLIENTE
                        && t.DataVencimento.Year == dataAniv.Year
                        && !t.IsDeleted);

                    if (!jaExiste)
                    {
                        _context.Tarefas.Add(new Tarefa
                        {
                            UsuarioId             = usuarioId,
                            ClienteId             = cliente.Id,
                            Titulo                = $"Aniversário: {cliente.Nome}",
                            Descricao             = $"Parabenize {cliente.Nome} pelo aniversário e aproveite para estreitar o relacionamento.",
                            DataVencimento        = dataAniv,
                            Tipo                  = TarefaTipo.Aniversario,
                            Prioridade            = TarefaPrioridade.Media,
                            Status                = TarefaStatus.Pendente,
                            Origem                = TarefaOrigem.Automatica,
                            GeradaAutomaticamente = true,
                            TemplateCodigo        = ANIVERSARIO_CLIENTE
                        });
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        // ── Scheduler-ready — sem dependência de HttpContext ─────────────────────

        public async Task ProcessarLembretesDoUsuarioAsync(Guid usuarioId)
        {
            try
            {
                // Gerar tarefas para todas as viagens aprovadas dos últimos 60 dias (catchup)
                var propostaIds = await _context.Propostas
                    .Where(p => (p.UsuarioResponsavelId == usuarioId || p.UsuarioMasterId == usuarioId)
                        && p.StatusProposta == StatusProposta.Aprovada
                        && p.DataFim.HasValue && p.DataFim.Value >= DateTime.Today.AddDays(-30))
                    .Select(p => p.Id)
                    .ToListAsync();

                foreach (var pid in propostaIds)
                    await GerarTarefasParaViagemAsync(pid);

                await ProcessarAniversariosDoUsuarioAsync(usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar lembretes do usuário {UsuarioId}", usuarioId);
            }
        }

        /// <summary>
        /// Ponto de entrada para schedulers externos (Hangfire, Azure Function, Worker Service).
        /// Processa todos os usuários ativos. Não depende de request ou sessão.
        /// </summary>
        public async Task ProcessarLembretesDoDiaAsync()
        {
            _logger.LogInformation("Processamento de lembretes do dia iniciado: {Data}", DateTime.Today);
            try
            {
                var usuariosIds = await _context.Usuarios
                    .Where(u => u.Status == StatusUsuario.Ativo || u.Status == StatusUsuario.Novo)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var uid in usuariosIds)
                    await ProcessarLembretesDoUsuarioAsync(uid);

                _logger.LogInformation("Processamento concluído. {Total} usuários processados.", usuariosIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento de lembretes do dia");
                throw;
            }
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        public async Task<List<Tarefa>> ListarPorUsuarioAsync(Guid usuarioId, DateTime? de = null, DateTime? ate = null, string? status = null)
        {
            var q = _context.Tarefas
                .Include(t => t.Cliente)
                .Include(t => t.Proposta)
                .Where(t => t.UsuarioId == usuarioId && !t.IsDeleted)
                .AsQueryable();

            if (de.HasValue)    q = q.Where(t => t.DataVencimento >= de.Value.Date);
            if (ate.HasValue)   q = q.Where(t => t.DataVencimento <= ate.Value.Date);
            if (status != null) q = q.Where(t => t.Status == status);

            return await q.OrderBy(t => t.DataVencimento).ThenByDescending(t => t.Prioridade).ToListAsync();
        }

        public async Task<List<Tarefa>> ListarHojeAsync(Guid usuarioId)
        {
            var hoje = DateTime.Today;
            return await _context.Tarefas
                .Include(t => t.Cliente)
                .Include(t => t.Proposta)
                .Where(t => t.UsuarioId == usuarioId && !t.IsDeleted
                    && t.Status == TarefaStatus.Pendente
                    && t.DataVencimento.Date == hoje)
                .OrderByDescending(t => t.Prioridade)
                .ToListAsync();
        }

        public async Task<List<Tarefa>> ListarAtrasadasAsync(Guid usuarioId)
        {
            var hoje = DateTime.Today;
            return await _context.Tarefas
                .Include(t => t.Cliente)
                .Include(t => t.Proposta)
                .Where(t => t.UsuarioId == usuarioId && !t.IsDeleted
                    && t.Status == TarefaStatus.Pendente
                    && t.DataVencimento.Date < hoje)
                .OrderBy(t => t.DataVencimento)
                .ToListAsync();
        }

        // ── Configurações ────────────────────────────────────────────────────────

        public async Task<List<ConfiguracaoLembrete>> ObterConfiguracoesAsync(Guid usuarioId)
        {
            await GarantirConfigsExistemAsync(usuarioId);
            return await _context.ConfiguracoesLembrete
                .Where(c => c.UsuarioId == usuarioId)
                .OrderBy(c => c.Tipo).ThenBy(c => c.TemplateCodigo)
                .ToListAsync();
        }

        public async Task SalvarConfiguracoesAsync(Guid usuarioId, List<(string TemplateCodigo, bool Habilitado)> configs)
        {
            var existentes = await _context.ConfiguracoesLembrete
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();

            foreach (var (codigo, habilitado) in configs)
            {
                var cfg = existentes.FirstOrDefault(c => c.TemplateCodigo == codigo);
                if (cfg is not null)
                {
                    cfg.Habilitado      = habilitado;
                    cfg.DataAtualizacao = DateTime.Now;
                }
            }
            await _context.SaveChangesAsync();
        }

        // ── Privados ─────────────────────────────────────────────────────────────

        private async Task TentarGerarAsync(
            Dictionary<string, ConfiguracaoLembrete> configs,
            string templateCodigo,
            Guid usuarioId, Guid? clienteId, Guid? propostaId,
            DateTime dataReferencia)
        {
            if (!configs.TryGetValue(templateCodigo, out var cfg) || !cfg.Habilitado) return;

            var dataVenc = dataReferencia.AddDays(cfg.OffsetDias ?? 0);
            // Não criar tarefas com data muito no passado (exceto feedback/indicação)
            if (dataVenc.Date < DateTime.Today.AddDays(-3)) dataVenc = DateTime.Today;

            if (!await JaExisteAsync(usuarioId, templateCodigo, propostaId, dataVenc))
                await CriarAutomaticaAsync(usuarioId, clienteId, propostaId, templateCodigo, dataVenc, cfg.Tipo);
        }

        private async Task<bool> JaExisteAsync(Guid usuarioId, string templateCodigo, Guid? propostaId, DateTime dataVencimento)
        {
            return await _context.Tarefas.AnyAsync(t =>
                t.UsuarioId == usuarioId
                && t.TemplateCodigo == templateCodigo
                && t.PropostaId == propostaId
                && t.DataVencimento.Date == dataVencimento.Date
                && !t.IsDeleted);
        }

        private async Task CriarAutomaticaAsync(Guid usuarioId, Guid? clienteId, Guid? propostaId, string templateCodigo, DateTime dataVencimento, string tipo)
        {
            _context.Tarefas.Add(new Tarefa
            {
                UsuarioId             = usuarioId,
                ClienteId             = clienteId,
                PropostaId            = propostaId,
                Titulo                = TituloTemplate(templateCodigo),
                Descricao             = DescricaoTemplate(templateCodigo),
                DataVencimento        = dataVencimento.Date,
                Tipo                  = tipo,
                Prioridade            = PrioridadeTemplate(templateCodigo),
                Status                = TarefaStatus.Pendente,
                Origem                = TarefaOrigem.Automatica,
                GeradaAutomaticamente = true,
                TemplateCodigo        = templateCodigo
            });
            await _context.SaveChangesAsync();
        }

        private async Task<Dictionary<string, ConfiguracaoLembrete>> ObterConfigsDicionario(Guid usuarioId)
        {
            await GarantirConfigsExistemAsync(usuarioId);
            var lista = await _context.ConfiguracoesLembrete
                .Where(c => c.UsuarioId == usuarioId)
                .AsNoTracking()
                .ToListAsync();
            return lista.ToDictionary(c => c.TemplateCodigo);
        }

        private async Task GarantirConfigsExistemAsync(Guid usuarioId)
        {
            var existentes = await _context.ConfiguracoesLembrete
                .Where(c => c.UsuarioId == usuarioId)
                .Select(c => c.TemplateCodigo)
                .ToListAsync();

            var novas = DefaultTemplates
                .Where(t => !existentes.Contains(t.Codigo))
                .Select(t => new ConfiguracaoLembrete
                {
                    UsuarioId         = usuarioId,
                    Tipo              = t.Tipo,
                    TemplateCodigo    = t.Codigo,
                    Habilitado        = t.HabPadrao,
                    OffsetDias        = t.OffsetDias,
                    MomentoReferencia = t.Momento
                })
                .ToList();

            if (novas.Count > 0)
            {
                _context.ConfiguracoesLembrete.AddRange(novas);
                await _context.SaveChangesAsync();
            }
        }

        // ── Textos dos templates ──────────────────────────────────────────────────

        public static string TituloTemplate(string codigo) => codigo switch
        {
            FOLLOWUP_VISUALIZACAO => "Follow-up: proposta visualizada",
            OFERECER_SEGURO       => "Oferecer seguro de viagem",
            REVISAR_VOUCHERS      => "Revisar vouchers e documentos",
            CHECKIN_VOO           => "Lembrar cliente do check-in",
            CONFIRMAR_HOTEL       => "Confirmar reserva do hotel",
            BOA_VIAGEM            => "Enviar mensagem de boa viagem",
            PEDIR_FEEDBACK        => "Pedir feedback da viagem",
            PEDIR_INDICACAO       => "Pedir indicação de novos clientes",
            ANIVERSARIO_CLIENTE   => "Aniversário do cliente",
            _                     => codigo
        };

        public static string DescricaoTemplate(string codigo) => codigo switch
        {
            FOLLOWUP_VISUALIZACAO => "O cliente visualizou a proposta. É um ótimo momento para entrar em contato!",
            OFERECER_SEGURO       => "Ofereça o seguro de viagem antes da data de saída.",
            REVISAR_VOUCHERS      => "Revise todos os vouchers, passaportes e documentos necessários.",
            CHECKIN_VOO           => "Avise o cliente sobre o check-in online disponível 24-48h antes do voo.",
            CONFIRMAR_HOTEL       => "Confirme a reserva do hotel e reenvie os dados de acesso ao cliente.",
            BOA_VIAGEM            => "Envie uma mensagem desejando uma ótima viagem ao cliente.",
            PEDIR_FEEDBACK        => "Entre em contato para ouvir como foi a experiência da viagem.",
            PEDIR_INDICACAO       => "Peça ao cliente satisfeito que indique amigos e familiares.",
            ANIVERSARIO_CLIENTE   => "Parabenize o cliente pelo aniversário.",
            _                     => ""
        };

        public static string TextoQuandoTemplate(string codigo, int? offsetDias, string? momento) => codigo switch
        {
            FOLLOWUP_VISUALIZACAO => "No mesmo dia em que a proposta for visualizada",
            ANIVERSARIO_CLIENTE   => "No dia do aniversário do cliente",
            _ => momento switch
            {
                MomentoReferenciaLembrete.AntesInicio => offsetDias.HasValue
                    ? $"{Math.Abs(offsetDias.Value)} dias antes do início da viagem"
                    : "Antes do início da viagem",
                MomentoReferenciaLembrete.AposFim => offsetDias.HasValue
                    ? $"{Math.Abs(offsetDias.Value)} dias após o fim da viagem"
                    : "Após o fim da viagem",
                _ => ""
            }
        };

        private static string PrioridadeTemplate(string codigo) => codigo switch
        {
            FOLLOWUP_VISUALIZACAO => TarefaPrioridade.Alta,
            OFERECER_SEGURO       => TarefaPrioridade.Alta,
            REVISAR_VOUCHERS      => TarefaPrioridade.Alta,
            CHECKIN_VOO           => TarefaPrioridade.Alta,
            CONFIRMAR_HOTEL       => TarefaPrioridade.Media,
            BOA_VIAGEM            => TarefaPrioridade.Media,
            PEDIR_FEEDBACK        => TarefaPrioridade.Media,
            PEDIR_INDICACAO       => TarefaPrioridade.Baixa,
            ANIVERSARIO_CLIENTE   => TarefaPrioridade.Media,
            _                     => TarefaPrioridade.Media
        };
    }
}
