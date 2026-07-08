using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using SistemaUsuarios.Models.Dto;

namespace SistemaUsuarios.Services
{
    public class ImportacaoPersistenciaService
    {
        private readonly ApplicationDbContext _context;

        public ImportacaoPersistenciaService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ResultadoImportacao> ImportarAsync(
            Guid propostaId,
            Guid usuarioId,
            bool isMaster,
            ImportacaoPreviewDto preview)
        {
            var proposta = await _context.Propostas
                .FirstOrDefaultAsync(p => p.Id == propostaId &&
                    (isMaster ? p.UsuarioMasterId == usuarioId : p.UsuarioResponsavelId == usuarioId));

            if (proposta == null)
                return Erro("Proposta não encontrada ou sem permissão.");

            var resultado = new ResultadoImportacao { Ok = true };

            // Atualizar dados básicos da proposta
            if (preview.Proposta?.Incluir == true)
            {
                if (!string.IsNullOrWhiteSpace(preview.Proposta.Titulo) &&
                    string.IsNullOrWhiteSpace(proposta.Titulo))
                    proposta.Titulo = preview.Proposta.Titulo.Trim();

                if (!string.IsNullOrWhiteSpace(preview.Proposta.ObservacoesGerais) &&
                    string.IsNullOrWhiteSpace(proposta.ObservacoesGerais))
                    proposta.ObservacoesGerais = preview.Proposta.ObservacoesGerais.Trim();

                proposta.DataModificacao = DateTime.Now;
            }

            // Passageiros
            foreach (var dto in preview.Passageiros.Where(p => p.Incluir && !string.IsNullOrWhiteSpace(p.Nome)))
            {
                var maxOrdem = await _context.PassageirosProposta
                    .Where(p => p.PropostaId == propostaId)
                    .MaxAsync(p => (int?)p.Ordem) ?? 0;

                _context.PassageirosProposta.Add(new PassageiroProposta
                {
                    Id = Guid.NewGuid(),
                    PropostaId = propostaId,
                    Nome = dto.Nome.Trim(),
                    DataNascimento = ParseData(dto.DataNascimento),
                    Genero = ParseEnum<Genero>(dto.Genero),
                    Relacionamento = ParseEnum<RelacionamentoPassageiro>(dto.Relacionamento),
                    Observacoes = dto.Observacoes?.Trim(),
                    Ordem = maxOrdem + 1,
                    DataCriacao = DateTime.Now
                });
                resultado.Passageiros++;
            }

            // Voos
            foreach (var dto in preview.Voos.Where(v => v.Incluir && !string.IsNullOrWhiteSpace(v.NumeroVoo)))
            {
                var maxOrdem = await _context.Voos
                    .Where(v => v.PropostaId == propostaId)
                    .MaxAsync(v => (int?)v.Ordem) ?? 0;

                _context.Voos.Add(new Voo
                {
                    Id = Guid.NewGuid(),
                    PropostaId = propostaId,
                    NumeroVoo = dto.NumeroVoo.Trim().ToUpperInvariant(),
                    TipoVoo = ParseEnum<TipoVoo>(dto.TipoVoo) ?? TipoVoo.Ida,
                    Companhia = dto.Companhia.Trim(),
                    Classe = dto.Classe?.Trim(),
                    Duracao = dto.Duracao?.Trim(),
                    Origem = dto.Origem.Trim(),
                    Destino = dto.Destino.Trim(),
                    HorarioSaida = ParseDateTime(dto.HorarioSaida),
                    HorarioChegada = ParseDateTime(dto.HorarioChegada),
                    BagagemMaoPeso = dto.BagagemMaoPeso,
                    BagagemDespachadaPeso = dto.BagagemDespachadaPeso,
                    Ordem = maxOrdem + 1,
                    DataCriacao = DateTime.Now
                });
                resultado.Voos++;
            }

            // Destinos + itens aninhados
            foreach (var dtoDestino in preview.Destinos.Where(d => d.Incluir && !string.IsNullOrWhiteSpace(d.Nome)))
            {
                var maxOrdemDestino = await _context.Destinos
                    .Where(d => d.PropostaId == propostaId)
                    .MaxAsync(d => (int?)d.Ordem) ?? 0;

                var destino = new Destino
                {
                    Id = Guid.NewGuid(),
                    PropostaId = propostaId,
                    Nome = dtoDestino.Nome.Trim(),
                    Pais = dtoDestino.Pais?.Trim(),
                    Cidade = dtoDestino.Cidade?.Trim(),
                    DataChegada = ParseData(dtoDestino.DataChegada),
                    DataSaida = ParseData(dtoDestino.DataSaida),
                    Descricao = dtoDestino.Descricao?.Trim(),
                    Latitude = dtoDestino.Latitude,
                    Longitude = dtoDestino.Longitude,
                    Localizacao = CriarPoint(dtoDestino.Latitude, dtoDestino.Longitude),
                    Ordem = maxOrdemDestino + 1,
                    DataCriacao = DateTime.Now
                };

                _context.Destinos.Add(destino);
                await _context.SaveChangesAsync();
                resultado.Destinos++;

                // Hospedagens
                foreach (var dtoHosp in dtoDestino.Hospedagens.Where(h => h.Incluir && !string.IsNullOrWhiteSpace(h.Nome)))
                {
                    var maxOrdemHosp = await _context.Hospedagens
                        .Where(h => h.DestinoId == destino.Id)
                        .MaxAsync(h => (int?)h.Ordem) ?? 0;

                    _context.Hospedagens.Add(new Hospedagem
                    {
                        Id = Guid.NewGuid(),
                        DestinoId = destino.Id,
                        Nome = dtoHosp.Nome.Trim(),
                        Descricao = dtoHosp.Descricao?.Trim(),
                        Endereco = dtoHosp.Endereco?.Trim(),
                        Latitude = dtoHosp.Latitude,
                        Longitude = dtoHosp.Longitude,
                        CheckIn = ParseData(dtoHosp.CheckIn),
                        CheckOut = ParseData(dtoHosp.CheckOut),
                        Categoria = ParseEnum<CategoriaHospedagem>(dtoHosp.Categoria) ?? CategoriaHospedagem.Hotel,
                        TipoPensao = ParseEnum<TipoPensao>(dtoHosp.TipoPensao) ?? TipoPensao.SemPensao,
                        Reserva = dtoHosp.Reserva?.Trim(),
                        Observacoes = dtoHosp.Observacoes?.Trim(),
                        Ordem = maxOrdemHosp + 1,
                        DataCriacao = DateTime.Now
                    });
                    resultado.Hospedagens++;
                }

                // Experiências
                foreach (var dtoExp in dtoDestino.Experiencias.Where(e => e.Incluir && !string.IsNullOrWhiteSpace(e.TipoPasseio)))
                {
                    var maxOrdemExp = await _context.Experiencias
                        .Where(e => e.DestinoId == destino.Id)
                        .MaxAsync(e => (int?)e.Ordem) ?? 0;

                    _context.Experiencias.Add(new Experiencia
                    {
                        Id = Guid.NewGuid(),
                        DestinoId = destino.Id,
                        TipoPasseio = dtoExp.TipoPasseio.Trim(),
                        Descricao = dtoExp.Descricao?.Trim(),
                        Valor = dtoExp.Valor,
                        DataInicio = ParseDateTime(dtoExp.DataInicio),
                        DataFim = ParseDateTime(dtoExp.DataFim),
                        Ordem = maxOrdemExp + 1,
                        DataCriacao = DateTime.Now
                    });
                    resultado.Experiencias++;
                }

                // Transportes
                foreach (var dtoTransp in dtoDestino.Transportes.Where(t => t.Incluir && !string.IsNullOrWhiteSpace(t.Titulo)))
                {
                    var maxOrdemTransp = await _context.Transportes
                        .Where(t => t.DestinoId == destino.Id)
                        .MaxAsync(t => (int?)t.Ordem) ?? 0;

                    _context.Transportes.Add(new Transporte
                    {
                        Id = Guid.NewGuid(),
                        DestinoId = destino.Id,
                        Titulo = dtoTransp.Titulo.Trim(),
                        Descricao = dtoTransp.Descricao?.Trim(),
                        Valor = dtoTransp.Valor,
                        Ordem = maxOrdemTransp + 1,
                        DataCriacao = DateTime.Now
                    });
                    resultado.Transportes++;
                }
            }

            // Seguros
            foreach (var dtoSeg in preview.Seguros.Where(s => s.Incluir && !string.IsNullOrWhiteSpace(s.Titulo)))
            {
                var maxOrdemSeg = await _context.Seguros
                    .Where(s => s.PropostaId == propostaId)
                    .MaxAsync(s => (int?)s.Ordem) ?? 0;

                _context.Seguros.Add(new Seguro
                {
                    Id = Guid.NewGuid(),
                    PropostaId = propostaId,
                    Titulo = dtoSeg.Titulo.Trim(),
                    Descricao = dtoSeg.Descricao?.Trim(),
                    Valor = dtoSeg.Valor,
                    Ordem = maxOrdemSeg + 1,
                    DataCriacao = DateTime.Now
                });
                resultado.Seguros++;
            }

            // Valores financeiros → appenda ao HTML de valores
            if (preview.ValoresFinanceiros?.Observacoes != null)
            {
                var obs = preview.ValoresFinanceiros.Observacoes;
                if (preview.ValoresFinanceiros.ValorTotal.HasValue)
                    obs = $"<strong>Valor total: R$ {preview.ValoresFinanceiros.ValorTotal:N2}</strong><br>{obs}";

                if (string.IsNullOrWhiteSpace(proposta.ValoresPropostaHtml))
                    proposta.ValoresPropostaHtml = $"<p>{obs}</p>";
            }

            await _context.SaveChangesAsync();
            return resultado;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static ResultadoImportacao Erro(string msg) =>
            new() { Ok = false, Erro = msg };

        private static DateTime? ParseData(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            return DateTime.TryParse(valor, out var d) ? d.Date : null;
        }

        private static DateTime? ParseDateTime(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            return DateTime.TryParse(valor, out var d) ? d : null;
        }

        private static T? ParseEnum<T>(string? valor) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            return Enum.TryParse<T>(valor, ignoreCase: true, out var result) ? result : null;
        }

        private static Point? CriarPoint(double? lat, double? lon)
        {
            if (!lat.HasValue || !lon.HasValue) return null;
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180) return null;
            return new Point(lon.Value, lat.Value) { SRID = 4326 };
        }
    }
}
