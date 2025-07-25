using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    public class PropostaListViewModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        [Display(Name = "Data de Criação")]
        public DateTime DataCriacao { get; set; }

        [Display(Name = "Data de Início")]
        public DateTime? DataInicio { get; set; }

        [Display(Name = "Data de Fim")]
        public DateTime? DataFim { get; set; }

        [Display(Name = "Número de Passageiros")]
        public int NumeroPassageiros { get; set; }

        [Display(Name = "Número de Crianças")]
        public int NumeroCriancas { get; set; }

        [Display(Name = "Status")]
        public StatusProposta StatusProposta { get; set; }

        [Display(Name = "Nome do Usuário")]
        public string NomeUsuario { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }

        [Display(Name = "Link Público Ativo")]
        public bool LinkPublicoAtivo { get; set; }

        [Display(Name = "Foto de Capa")]
        public string? FotoCapa { get; set; }

        [Display(Name = "Data de Modificação")]
        public DateTime? DataModificacao { get; set; }

        // === PROPRIEDADES CALCULADAS ===

        /// <summary>
        /// Total de pessoas (adultos + crianças)
        /// </summary>
        public int TotalPessoas => NumeroPassageiros + NumeroCriancas;

        /// <summary>
        /// Duração da viagem em dias
        /// </summary>
        public int? DuracaoDias
        {
            get
            {
                if (DataInicio.HasValue && DataFim.HasValue)
                    return (DataFim.Value - DataInicio.Value).Days + 1;
                return null;
            }
        }

        /// <summary>
        /// Texto do status da proposta
        /// </summary>
        public string StatusPropostaTexto
        {
            get
            {
                return StatusProposta switch
                {
                    StatusProposta.Rascunho => "Rascunho",
                    StatusProposta.Enviada => "Enviada",
                    StatusProposta.Aprovada => "Aprovada",
                    StatusProposta.Rejeitada => "Rejeitada",
                    StatusProposta.Cancelada => "Cancelada",
                    _ => "Desconhecido"
                };
            }
        }

        /// <summary>
        /// Classe CSS para cor do badge de status
        /// </summary>
        public string StatusPropostaCor
        {
            get
            {
                return StatusProposta switch
                {
                    StatusProposta.Rascunho => "secondary",
                    StatusProposta.Enviada => "warning",
                    StatusProposta.Aprovada => "success",
                    StatusProposta.Rejeitada => "danger",
                    StatusProposta.Cancelada => "dark",
                    _ => "secondary"
                };
            }
        }

        /// <summary>
        /// Indica se a proposta tem foto de capa
        /// </summary>
        public bool TemFotoCapa => !string.IsNullOrEmpty(FotoCapa);

        /// <summary>
        /// Período formatado da viagem
        /// </summary>
        public string PeriodoFormatado
        {
            get
            {
                if (DataInicio.HasValue && DataFim.HasValue)
                {
                    var dias = DuracaoDias ?? 0;
                    return $"{DataInicio.Value:dd/MM} - {DataFim.Value:dd/MM/yyyy} ({dias} {(dias == 1 ? "dia" : "dias")})";
                }
                else if (DataInicio.HasValue)
                {
                    return $"A partir de {DataInicio.Value:dd/MM/yyyy}";
                }
                else if (DataFim.HasValue)
                {
                    return $"Até {DataFim.Value:dd/MM/yyyy}";
                }
                return "Período não definido";
            }
        }

        /// <summary>
        /// Passageiros formatado
        /// </summary>
        public string PassageirosFormatado
        {
            get
            {
                var total = TotalPessoas;
                var texto = $"{total} pessoa{(total != 1 ? "s" : "")}";

                if (NumeroCriancas > 0)
                {
                    texto += $" ({NumeroPassageiros} adulto{(NumeroPassageiros != 1 ? "s" : "")} + {NumeroCriancas} criança{(NumeroCriancas != 1 ? "s" : "")})";
                }

                return texto;
            }
        }

        /// <summary>
        /// URL do link de compartilhamento
        /// </summary>
        public string LinkCompartilhamento => $"/Proposta/Publico/{Id}";

        /// <summary>
        /// Status do link público formatado
        /// </summary>
        public string StatusLinkPublico => LinkPublicoAtivo ? "Ativo" : "Inativo";

        /// <summary>
        /// Classe CSS para status do link público
        /// </summary>
        public string ClasseStatusLinkPublico => LinkPublicoAtivo ? "text-success" : "text-muted";

        /// <summary>
        /// Ícone para status do link público
        /// </summary>
        public string IconeStatusLinkPublico => LinkPublicoAtivo ? "fas fa-check-circle" : "fas fa-times-circle";

        /// <summary>
        /// Indica quantos dias desde a criação
        /// </summary>
        public int DiasDesdeCreacao => (DateTime.Now - DataCriacao).Days;

        /// <summary>
        /// Indica quantos dias desde a última modificação
        /// </summary>
        public int DiasDesdeModificacao
        {
            get
            {
                var dataRef = DataModificacao ?? DataCriacao;
                return (DateTime.Now - dataRef).Days;
            }
        }

        /// <summary>
        /// Indica se a proposta precisa de atenção (sem modificação há muito tempo)
        /// </summary>
        public bool PrecisaAtencao
        {
            get
            {
                if (StatusProposta == StatusProposta.Aprovada ||
                    StatusProposta == StatusProposta.Rejeitada ||
                    StatusProposta == StatusProposta.Cancelada)
                    return false;

                return DiasDesdeModificacao > 7;
            }
        }

        /// <summary>
        /// Cor do indicador de atenção
        /// </summary>
        public string CorIndicadorAtencao
        {
            get
            {
                if (!PrecisaAtencao) return "success";

                return DiasDesdeModificacao switch
                {
                    > 14 => "danger",
                    > 7 => "warning",
                    _ => "info"
                };
            }
        }

        /// <summary>
        /// Texto do indicador de atenção
        /// </summary>
        public string TextoIndicadorAtencao
        {
            get
            {
                if (!PrecisaAtencao) return "Em dia";

                return DiasDesdeModificacao switch
                {
                    > 14 => "Atenção urgente",
                    > 7 => "Precisa atenção",
                    _ => "Recente"
                };
            }
        }

        /// <summary>
        /// Título truncado para exibição em cards
        /// </summary>
        /// <param name="maxLength">Tamanho máximo</param>
        /// <returns>Título truncado</returns>
        public string TituloTruncado(int maxLength = 50)
        {
            if (Titulo.Length <= maxLength)
                return Titulo;

            return Titulo.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Verifica se a proposta pode ser editada
        /// </summary>
        public bool PodeSerEditada
        {
            get
            {
                // Propostas canceladas não podem ser editadas
                return StatusProposta != StatusProposta.Cancelada;
            }
        }

        /// <summary>
        /// Verifica se o status pode ser alterado
        /// </summary>
        public bool PodeAlterarStatus => PodeSerEditada;

        /// <summary>
        /// Lista de ações disponíveis baseadas no status atual
        /// </summary>
        public List<string> AcoesDisponiveis
        {
            get
            {
                var acoes = new List<string> { "Visualizar", "Editar" };

                switch (StatusProposta)
                {
                    case StatusProposta.Rascunho:
                        acoes.AddRange(new[] { "Enviar", "Duplicar", "Excluir" });
                        break;
                    case StatusProposta.Enviada:
                        acoes.AddRange(new[] { "Aprovar", "Rejeitar", "Duplicar" });
                        break;
                    case StatusProposta.Aprovada:
                        acoes.AddRange(new[] { "Analytics", "Compartilhar", "Duplicar" });
                        break;
                    case StatusProposta.Rejeitada:
                        acoes.AddRange(new[] { "Reenviar", "Duplicar", "Cancelar" });
                        break;
                    case StatusProposta.Cancelada:
                        acoes.AddRange(new[] { "Duplicar" });
                        break;
                }

                if (LinkPublicoAtivo)
                {
                    acoes.Add("Ver Público");
                    acoes.Add("Copiar Link");
                }

                return acoes;
            }
        }

        /// <summary>
        /// Progresso de completude da proposta (0-100)
        /// </summary>
        public int ProgressoCompletude
        {
            get
            {
                var pontos = 0;
                var totalPontos = 6;

                if (!string.IsNullOrEmpty(Titulo)) pontos++;
                if (NumeroPassageiros > 0) pontos++;
                if (DataInicio.HasValue) pontos++;
                if (DataFim.HasValue) pontos++;
                if (TemFotoCapa) pontos++;
                if (LinkPublicoAtivo) pontos++;

                return (int)Math.Round((double)pontos / totalPontos * 100);
            }
        }

        /// <summary>
        /// Classe CSS para barra de progresso de completude
        /// </summary>
        public string ClasseProgressoCompletude
        {
            get
            {
                var progresso = ProgressoCompletude;
                return progresso switch
                {
                    >= 80 => "bg-success",
                    >= 60 => "bg-info",
                    >= 40 => "bg-warning",
                    _ => "bg-danger"
                };
            }
        }

        /// <summary>
        /// Converte para string para debug
        /// </summary>
        /// <returns>Representação string do objeto</returns>
        public override string ToString()
        {
            return $"Proposta: {TituloTruncado(30)} - Status: {StatusPropostaTexto} - Passageiros: {TotalPessoas}";
        }
    }
}