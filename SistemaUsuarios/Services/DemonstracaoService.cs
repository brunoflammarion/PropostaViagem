using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Services
{
    public class DemonstracaoService
    {
        private readonly ApplicationDbContext _db;
        private readonly BlobStorageService   _blob;
        private readonly ILogger<DemonstracaoService> _log;

        public DemonstracaoService(
            ApplicationDbContext db,
            BlobStorageService blob,
            ILogger<DemonstracaoService> log)
        {
            _db   = db;
            _blob = blob;
            _log  = log;
        }

        // ── Chamado no onboarding de novas agências ────────────────────────────
        public async Task AplicarConteudosAsync(Guid masterId)
        {
            var modelos = await _db.ConteudosDemonstracao
                .Where(c => c.Ativo && c.AplicarAutomaticamente)
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            foreach (var modelo in modelos)
            {
                var jaAplicado = await _db.ConteudosDemonstracaoAplicados
                    .AnyAsync(a => a.ConteudoDemonstracaoId == modelo.Id
                               && a.AgenciaMasterId == masterId);
                if (jaAplicado) continue;

                Guid? clonadaId = null;
                var status      = StatusAplicacaoDemonstracao.Sucesso;
                string? erro    = null;

                try
                {
                    clonadaId = modelo.TipoConteudo == TipoConteudoDemonstracao.Proposta
                        ? await ClonarPropostaAsync(modelo.EntidadeOrigemId, masterId, modelo.Id)
                        : await ClonarOfertaAsync(modelo.EntidadeOrigemId, masterId, modelo.Id);
                }
                catch (Exception ex)
                {
                    status = StatusAplicacaoDemonstracao.Falha;
                    erro   = ex.Message;
                    _log.LogWarning(ex, "Demonstração {ModeloId} falhou para agência {MasterId}", modelo.Id, masterId);
                }

                _db.ConteudosDemonstracaoAplicados.Add(new ConteudoDemonstracaoAplicado
                {
                    ConteudoDemonstracaoId = modelo.Id,
                    AgenciaMasterId        = masterId,
                    EntidadeClonadaId      = clonadaId,
                    DataAplicacao          = DateTime.Now,
                    StatusAplicacao        = status,
                    MensagemErro           = erro,
                });
                await _db.SaveChangesAsync();
            }
        }

        // ── Reaplicar apenas os que falharam (Platform Admin) ─────────────────
        public async Task ReaplicarPendentesAsync(Guid masterId)
        {
            var modelos = await _db.ConteudosDemonstracao
                .Where(c => c.Ativo)
                .OrderBy(c => c.Ordem)
                .ToListAsync();

            foreach (var modelo in modelos)
            {
                var aplicado = await _db.ConteudosDemonstracaoAplicados
                    .FirstOrDefaultAsync(a => a.ConteudoDemonstracaoId == modelo.Id
                                           && a.AgenciaMasterId == masterId);

                if (aplicado != null && aplicado.StatusAplicacao == StatusAplicacaoDemonstracao.Sucesso)
                    continue;

                Guid? clonadaId = null;
                var status      = StatusAplicacaoDemonstracao.Sucesso;
                string? erro    = null;

                try
                {
                    clonadaId = modelo.TipoConteudo == TipoConteudoDemonstracao.Proposta
                        ? await ClonarPropostaAsync(modelo.EntidadeOrigemId, masterId, modelo.Id)
                        : await ClonarOfertaAsync(modelo.EntidadeOrigemId, masterId, modelo.Id);
                }
                catch (Exception ex)
                {
                    status = StatusAplicacaoDemonstracao.Falha;
                    erro   = ex.Message;
                    _log.LogWarning(ex, "Reaplicação demonstração {ModeloId} falhou para agência {MasterId}", modelo.Id, masterId);
                }

                if (aplicado != null)
                {
                    aplicado.EntidadeClonadaId = clonadaId;
                    aplicado.DataAplicacao      = DateTime.Now;
                    aplicado.StatusAplicacao    = status;
                    aplicado.MensagemErro       = erro;
                }
                else
                {
                    _db.ConteudosDemonstracaoAplicados.Add(new ConteudoDemonstracaoAplicado
                    {
                        ConteudoDemonstracaoId = modelo.Id,
                        AgenciaMasterId        = masterId,
                        EntidadeClonadaId      = clonadaId,
                        DataAplicacao          = DateTime.Now,
                        StatusAplicacao        = status,
                        MensagemErro           = erro,
                    });
                }
                await _db.SaveChangesAsync();
            }
        }

        // ── Clone de Proposta ──────────────────────────────────────────────────
        private async Task<Guid> ClonarPropostaAsync(Guid origemId, Guid masterId, Guid modeloId)
        {
            var o = await _db.Propostas
                .Include(p => p.Destinos).ThenInclude(d => d.Fotos)
                .Include(p => p.Destinos).ThenInclude(d => d.Hospedagens).ThenInclude(h => h.Fotos)
                .Include(p => p.Destinos).ThenInclude(d => d.Hospedagens).ThenInclude(h => h.Comodidades)
                .Include(p => p.Destinos).ThenInclude(d => d.Hospedagens).ThenInclude(h => h.Acomodacoes).ThenInclude(a => a.Fotos)
                .Include(p => p.Destinos).ThenInclude(d => d.Hospedagens).ThenInclude(h => h.Acomodacoes).ThenInclude(a => a.Comodidades)
                .Include(p => p.Destinos).ThenInclude(d => d.Experiencias).ThenInclude(e => e.Imagens)
                .Include(p => p.Destinos).ThenInclude(d => d.Transportes).ThenInclude(t => t.Imagens)
                .Include(p => p.Voos)
                .Include(p => p.Seguros).ThenInclude(s => s.Imagens)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == origemId)
                ?? throw new InvalidOperationException($"Proposta de demonstração {origemId} não encontrada.");

            var nova = new Proposta
            {
                Id                           = Guid.NewGuid(),
                UsuarioId                    = masterId,
                UsuarioMasterId              = masterId,
                UsuarioResponsavelId         = masterId,
                Titulo                       = o.Titulo,
                DataCriacao                  = DateTime.Now,
                DataModificacao              = null,
                DataInicio                   = o.DataInicio,
                DataFim                      = o.DataFim,
                NumeroPassageiros            = o.NumeroPassageiros,
                NumeroCriancas               = o.NumeroCriancas,
                LayoutId                     = o.LayoutId,
                ObservacoesGerais            = o.ObservacoesGerais,
                StatusProposta               = StatusProposta.Rascunho,
                LinkPublicoAtivo             = false,
                DataExpiracaoLink            = null,
                CodigoAcesso                 = null,
                ClienteId                    = null,
                ResumoProposta               = o.ResumoProposta,
                CondicoesPropostaHtml        = o.CondicoesPropostaHtml,
                ValoresPropostaHtml          = o.ValoresPropostaHtml,
                SolicitarAvaliacaoHospedagem = o.SolicitarAvaliacaoHospedagem,
                SolicitarAvaliacaoAcomodacao = o.SolicitarAvaliacaoAcomodacao,
                SolicitarAvaliacaoExperiencia= o.SolicitarAvaliacaoExperiencia,
                IsConteudoDemonstracao       = true,
                ConteudoDemonstracaoOrigemId = modeloId,
                FotoCapa = await _blob.CopiarAsync(o.FotoCapa, "propostas"),
            };

            foreach (var d in o.Destinos)
            {
                var novoDestino = new Destino
                {
                    Id                     = Guid.NewGuid(),
                    PropostaId             = nova.Id,
                    Nome                   = d.Nome,
                    Descricao              = d.Descricao,
                    DataChegada            = d.DataChegada,
                    DataSaida              = d.DataSaida,
                    Ordem                  = d.Ordem,
                    Pais                   = d.Pais,
                    Cidade                 = d.Cidade,
                    DataCriacao            = DateTime.Now,
                    Latitude               = d.Latitude,
                    Longitude              = d.Longitude,
                    Localizacao            = d.Localizacao,
                    DescricaoLLM           = d.DescricaoLLM,
                    AtracoesLLM            = d.AtracoesLLM,
                    GastronomiaLLM         = d.GastronomiaLLM,
                    InformacoesPraticasLLM = d.InformacoesPraticasLLM,
                    MalaViagemLLM          = d.MalaViagemLLM,
                    CuidadosLLM            = d.CuidadosLLM,
                    PratosTipicosLLM       = d.PratosTipicosLLM,
                };

                foreach (var f in d.Fotos)
                    novoDestino.Fotos.Add(new DestinoFoto
                    {
                        Id          = Guid.NewGuid(),
                        DestinoId   = novoDestino.Id,
                        CaminhoFoto = await _blob.CopiarAsync(f.CaminhoFoto, "destinos") ?? f.CaminhoFoto,
                        Descricao   = f.Descricao,
                        Principal   = f.Principal,
                        Ordem       = f.Ordem,
                    });

                foreach (var h in d.Hospedagens)
                {
                    var novaHosp = new Hospedagem
                    {
                        Id          = Guid.NewGuid(),
                        DestinoId   = novoDestino.Id,
                        Nome        = h.Nome,
                        Descricao   = h.Descricao,
                        Endereco    = h.Endereco,
                        Latitude    = h.Latitude,
                        Longitude   = h.Longitude,
                        CheckIn     = h.CheckIn,
                        CheckOut    = h.CheckOut,
                        Categoria   = h.Categoria,
                        TipoPensao  = h.TipoPensao,
                        Reserva     = null, // dado operacional — não copiar
                        Observacoes = h.Observacoes,
                        Ordem       = h.Ordem,
                        DataCriacao = DateTime.Now,
                    };

                    foreach (var hf in h.Fotos)
                        novaHosp.Fotos.Add(new HospedagemFoto
                        {
                            Id           = Guid.NewGuid(),
                            HospedagemId = novaHosp.Id,
                            CaminhoFoto  = await _blob.CopiarAsync(hf.CaminhoFoto, "hospedagens") ?? hf.CaminhoFoto,
                            Descricao    = hf.Descricao,
                            Principal    = hf.Principal,
                            Ordem        = hf.Ordem,
                        });

                    foreach (var c in h.Comodidades)
                        novaHosp.Comodidades.Add(new HospedagemComodidade
                        {
                            Id           = Guid.NewGuid(),
                            HospedagemId = novaHosp.Id,
                            Nome         = c.Nome,
                            Ordem        = c.Ordem,
                        });

                    foreach (var a in h.Acomodacoes)
                    {
                        var novaAcom = new Acomodacao
                        {
                            Id           = Guid.NewGuid(),
                            HospedagemId = novaHosp.Id,
                            Nome         = a.Nome,
                            Descricao    = a.Descricao,
                            Ordem        = a.Ordem,
                        };

                        foreach (var af in a.Fotos)
                            novaAcom.Fotos.Add(new AcomodacaoFoto
                            {
                                Id           = Guid.NewGuid(),
                                AcomodacaoId = novaAcom.Id,
                                CaminhoFoto  = await _blob.CopiarAsync(af.CaminhoFoto, "acomodacoes") ?? af.CaminhoFoto,
                                Descricao    = af.Descricao,
                                Principal    = af.Principal,
                                Ordem        = af.Ordem,
                            });

                        foreach (var ac in a.Comodidades)
                            novaAcom.Comodidades.Add(new AcomodacaoComodidade
                            {
                                Id           = Guid.NewGuid(),
                                AcomodacaoId = novaAcom.Id,
                                Nome         = ac.Nome,
                                Ordem        = ac.Ordem,
                            });

                        novaHosp.Acomodacoes.Add(novaAcom);
                    }

                    novoDestino.Hospedagens.Add(novaHosp);
                }

                foreach (var e in d.Experiencias)
                {
                    var novaExp = new Experiencia
                    {
                        Id          = Guid.NewGuid(),
                        DestinoId   = novoDestino.Id,
                        TipoPasseio = e.TipoPasseio,
                        Descricao   = e.Descricao,
                        VideoUrl    = e.VideoUrl,
                        Valor       = e.Valor,
                        DataInicio  = e.DataInicio,
                        DataFim     = e.DataFim,
                        Ordem       = e.Ordem,
                        DataCriacao = DateTime.Now,
                    };

                    foreach (var img in e.Imagens)
                        novaExp.Imagens.Add(new ExperienciaImagem
                        {
                            Id            = Guid.NewGuid(),
                            ExperienciaId = novaExp.Id,
                            CaminhoImagem = await _blob.CopiarAsync(img.CaminhoImagem, "experiencias") ?? img.CaminhoImagem,
                            Descricao     = img.Descricao,
                            Ordem         = img.Ordem,
                        });

                    novoDestino.Experiencias.Add(novaExp);
                }

                foreach (var t in d.Transportes)
                {
                    var novoTransp = new Transporte
                    {
                        Id          = Guid.NewGuid(),
                        DestinoId   = novoDestino.Id,
                        Titulo      = t.Titulo,
                        Descricao   = t.Descricao,
                        Valor       = t.Valor,
                        Ordem       = t.Ordem,
                        DataCriacao = DateTime.Now,
                    };

                    foreach (var img in t.Imagens)
                        novoTransp.Imagens.Add(new TransporteImagem
                        {
                            Id           = Guid.NewGuid(),
                            TransporteId = novoTransp.Id,
                            CaminhoImagem= await _blob.CopiarAsync(img.CaminhoImagem, "transportes") ?? img.CaminhoImagem,
                            Descricao    = img.Descricao,
                            Principal    = img.Principal,
                            Ordem        = img.Ordem,
                        });

                    novoDestino.Transportes.Add(novoTransp);
                }

                nova.Destinos.Add(novoDestino);
            }

            foreach (var v in o.Voos)
                nova.Voos.Add(new Voo
                {
                    Id                           = Guid.NewGuid(),
                    PropostaId                   = nova.Id,
                    NumeroVoo                    = v.NumeroVoo,
                    TipoVoo                      = v.TipoVoo,
                    Companhia                    = v.Companhia,
                    Classe                       = v.Classe,
                    Duracao                      = v.Duracao,
                    Origem                       = v.Origem,
                    Destino                      = v.Destino,
                    HorarioSaida                 = v.HorarioSaida,
                    HorarioChegada               = v.HorarioChegada,
                    BagagemItemPessoalDescricao  = v.BagagemItemPessoalDescricao,
                    BagagemItemPessoalMedidas    = v.BagagemItemPessoalMedidas,
                    BagagemMaoPeso               = v.BagagemMaoPeso,
                    BagagemMaoMedidas            = v.BagagemMaoMedidas,
                    BagagemDespachadaPeso        = v.BagagemDespachadaPeso,
                    BagagemDespachadaMedidas     = v.BagagemDespachadaMedidas,
                    Observacao                   = v.Observacao,
                    ObservacaoImagemPath         = await _blob.CopiarAsync(v.ObservacaoImagemPath, "voos"),
                    Ordem                        = v.Ordem,
                    DataCriacao                  = DateTime.Now,
                });

            foreach (var s in o.Seguros)
            {
                var novoSeg = new Seguro
                {
                    Id          = Guid.NewGuid(),
                    PropostaId  = nova.Id,
                    Titulo      = s.Titulo,
                    Descricao   = s.Descricao,
                    Valor       = s.Valor,
                    Ordem       = s.Ordem,
                    DataCriacao = DateTime.Now,
                };

                foreach (var img in s.Imagens)
                    novoSeg.Imagens.Add(new SeguroImagem
                    {
                        Id            = Guid.NewGuid(),
                        SeguroId      = novoSeg.Id,
                        CaminhoImagem = await _blob.CopiarAsync(img.CaminhoImagem, "seguros") ?? img.CaminhoImagem,
                        Descricao     = img.Descricao,
                        Ordem         = img.Ordem,
                    });

                nova.Seguros.Add(novoSeg);
            }

            _db.Propostas.Add(nova);
            await _db.SaveChangesAsync();
            return nova.Id;
        }

        // ── Clone de Oferta ────────────────────────────────────────────────────
        private async Task<Guid> ClonarOfertaAsync(Guid origemId, Guid masterId, Guid modeloId)
        {
            var o = await _db.Ofertas
                .AsNoTracking()
                .FirstOrDefaultAsync(of => of.Id == origemId)
                ?? throw new InvalidOperationException($"Oferta de demonstração {origemId} não encontrada.");

            var nova = new Oferta
            {
                Id                           = Guid.NewGuid(),
                UsuarioId                    = masterId,
                UsuarioMasterId              = masterId,
                Nome                         = o.Nome,
                TemplateId                   = o.TemplateId,
                Cor1                         = o.Cor1,
                Cor2                         = o.Cor2,
                Cor3                         = o.Cor3,
                SlotsJson                    = o.SlotsJson,
                TituloPrincipal              = o.TituloPrincipal,
                Subtitulo                    = o.Subtitulo,
                DescricaoCurta               = o.DescricaoCurta,
                TextoComplementar            = o.TextoComplementar,
                Rodape                       = o.Rodape,
                Cta                          = o.Cta,
                Preco                        = o.Preco,
                PrecoAnterior                = o.PrecoAnterior,
                TextoAPartirDe               = o.TextoAPartirDe,
                CondicaoEspecial             = o.CondicaoEspecial,
                Parcelamento                 = o.Parcelamento,
                TextoUrgencia                = o.TextoUrgencia,
                ValidadeOferta               = o.ValidadeOferta,
                Destino                      = o.Destino,
                Origem                       = o.Origem,
                PeriodoViagem                = o.PeriodoViagem,
                QtdNoites                    = o.QtdNoites,
                CompanhiaAerea               = o.CompanhiaAerea,
                Hotel                        = o.Hotel,
                RegimeHospedagem             = o.RegimeHospedagem,
                InclusoesOferta              = o.InclusoesOferta,
                ObservacoesCurtas            = o.ObservacoesCurtas,
                RegrasCondicoes              = o.RegrasCondicoes,
                WhatsApp                     = o.WhatsApp,
                Telefone                     = o.Telefone,
                Email                        = o.Email,
                Instagram                    = o.Instagram,
                Site                         = o.Site,
                NomeAgencia                  = o.NomeAgencia,
                SeloPromocional              = o.SeloPromocional,
                TagPromocional               = o.TagPromocional,
                TextoInstitucional           = o.TextoInstitucional,
                DataCriacao                  = DateTime.Now,
                DataModificacao              = null,
                IsConteudoDemonstracao       = true,
                ConteudoDemonstracaoOrigemId = modeloId,
                ImagemPrincipalPath = await _blob.CopiarAsync(o.ImagemPrincipalPath, "ofertas"),
                LogoPath            = await _blob.CopiarAsync(o.LogoPath, "ofertas"),
            };

            _db.Ofertas.Add(nova);
            await _db.SaveChangesAsync();
            return nova.Id;
        }
    }
}
