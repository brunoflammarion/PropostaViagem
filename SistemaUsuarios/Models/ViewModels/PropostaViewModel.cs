using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SistemaUsuarios.Models.ViewModels
{
    public class PropostaViewModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Título é obrigatório")]
        [StringLength(500, ErrorMessage = "Título deve ter no máximo 500 caracteres")]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = string.Empty;

        public Guid? UsuarioId { get; set; }

        [Display(Name = "Data de Início")]
        [DataType(DataType.Date)]
        public DateTime? DataInicio { get; set; }

        [Display(Name = "Data de Fim")]
        [DataType(DataType.Date)]
        public DateTime? DataFim { get; set; }

        [Required(ErrorMessage = "Número de passageiros é obrigatório")]
        [Range(1, 50, ErrorMessage = "Número de passageiros deve estar entre 1 e 50")]
        [Display(Name = "Número de Passageiros")]
        public int NumeroPassageiros { get; set; } = 1;

        [Range(0, 20, ErrorMessage = "Número de crianças deve estar entre 0 e 20")]
        [Display(Name = "Número de Crianças")]
        public int NumeroCriancas { get; set; } = 0;

        [StringLength(1000, ErrorMessage = "URL da foto deve ter no máximo 1000 caracteres")]
        [Display(Name = "Foto de Capa (URL)")]
        public string? FotoCapa { get; set; }

        [Display(Name = "Upload de Foto de Capa")]
        public IFormFile? FotoCapaUpload { get; set; }

        [Display(Name = "Layout")]
        public int? LayoutId { get; set; }

        [Display(Name = "Observações Gerais")]
        [StringLength(2000, ErrorMessage = "Observações devem ter no máximo 2000 caracteres")]
        public string? ObservacoesGerais { get; set; }

        [Display(Name = "Status")]
        public StatusProposta StatusProposta { get; set; } = StatusProposta.Rascunho;

        [Display(Name = "Link Público Ativo")]
        public bool LinkPublicoAtivo { get; set; } = true;

        [Display(Name = "Data de Expiração do Link")]
        [DataType(DataType.DateTime)]
        public DateTime? DataExpiracaoLink { get; set; }

        // Campos somente leitura para informações do sistema
        public DateTime? DataCriacao { get; set; }
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
        /// Indica se a proposta tem observações
        /// </summary>
        public bool TemObservacoes => !string.IsNullOrEmpty(ObservacoesGerais);

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
        /// URL do link público da proposta
        /// </summary>
        public string? LinkPublico { get; set; }

        /// <summary>
        /// URL do link de compartilhamento
        /// </summary>
        public string? LinkCompartilhamento => Id.HasValue ? $"/Proposta/Publico/{Id}" : null;

        /// <summary>
        /// Indica se é uma nova proposta (criação)
        /// </summary>
        public bool IsNovaProposta => !Id.HasValue;

        /// <summary>
        /// Indica se é edição de proposta existente
        /// </summary>
        public bool IsEdicao => Id.HasValue;

        /// <summary>
        /// Título da página baseado no contexto
        /// </summary>
        public string TituloPagina => IsNovaProposta ? "Nova Proposta de Viagem" : "Editar Proposta de Viagem";

        /// <summary>
        /// Subtítulo da página baseado no contexto
        /// </summary>
        public string SubtituloPagina => IsNovaProposta
            ? "Crie uma proposta personalizada em duas etapas simples"
            : "Atualize as informações da sua proposta personalizada";

        /// <summary>
        /// Texto do botão principal baseado no contexto
        /// </summary>
        public string TextoBotaoPrincipal => IsNovaProposta ? "Criar Proposta" : "Salvar Alterações";

        /// <summary>
        /// Ícone do botão principal baseado no contexto
        /// </summary>
        public string IconeBotaoPrincipal => IsNovaProposta ? "fas fa-plus" : "fas fa-save";

        /// <summary>
        /// Indica se o link público está expirado
        /// </summary>
        public bool LinkPublicoExpirado
        {
            get
            {
                if (!LinkPublicoAtivo || !DataExpiracaoLink.HasValue)
                    return false;

                return DataExpiracaoLink.Value < DateTime.Now;
            }
        }

        /// <summary>
        /// Status do link público formatado
        /// </summary>
        public string StatusLinkPublico
        {
            get
            {
                if (!LinkPublicoAtivo)
                    return "Inativo";

                if (LinkPublicoExpirado)
                    return "Expirado";

                if (DataExpiracaoLink.HasValue)
                    return $"Ativo até {DataExpiracaoLink.Value:dd/MM/yyyy HH:mm}";

                return "Ativo";
            }
        }

        /// <summary>
        /// Classe CSS para status do link público
        /// </summary>
        public string ClasseStatusLinkPublico
        {
            get
            {
                if (!LinkPublicoAtivo || LinkPublicoExpirado)
                    return "text-danger";

                return "text-success";
            }
        }

        /// <summary>
        /// Ícone para status do link público
        /// </summary>
        public string IconeStatusLinkPublico
        {
            get
            {
                if (!LinkPublicoAtivo)
                    return "fas fa-link-slash";

                if (LinkPublicoExpirado)
                    return "fas fa-clock";

                return "fas fa-check-circle";
            }
        }

        // === MÉTODOS DE VALIDAÇÃO CUSTOMIZADA ===

        /// <summary>
        /// Valida se as datas estão em ordem cronológica
        /// </summary>
        /// <returns>True se válidas, False caso contrário</returns>
        public bool ValidarDatas()
        {
            if (DataInicio.HasValue && DataFim.HasValue)
            {
                return DataInicio.Value <= DataFim.Value;
            }
            return true; // Datas opcionais são sempre válidas
        }

        /// <summary>
        /// Obtém mensagem de erro para datas inválidas
        /// </summary>
        /// <returns>Mensagem de erro ou null se válidas</returns>
        public string? ObterErroValidacaoDatas()
        {
            if (!ValidarDatas())
            {
                return "Data de fim deve ser posterior ou igual à data de início";
            }
            return null;
        }

        /// <summary>
        /// Indica se a proposta pode ser editada
        /// </summary>
        public bool PodeSerEditada
        {
            get
            {
                // Propostas aprovadas ou rejeitadas podem ter restrições
                return StatusProposta != StatusProposta.Cancelada;
            }
        }

        /// <summary>
        /// Indica se a proposta pode ter o status alterado
        /// </summary>
        public bool PodeAlterarStatus => IsEdicao;

        /// <summary>
        /// Lista de status disponíveis para seleção
        /// </summary>
        public Dictionary<int, string> StatusDisponiveis
        {
            get
            {
                var status = new Dictionary<int, string>
                {
                    { (int)StatusProposta.Rascunho, "Rascunho" },
                    { (int)StatusProposta.Enviada, "Enviada" },
                    { (int)StatusProposta.Aprovada, "Aprovada" },
                    { (int)StatusProposta.Rejeitada, "Rejeitada" },
                    { (int)StatusProposta.Cancelada, "Cancelada" }
                };

                // Se for nova proposta, só mostrar Rascunho
                if (IsNovaProposta)
                {
                    return new Dictionary<int, string>
                    {
                        { (int)StatusProposta.Rascunho, "Rascunho" }
                    };
                }

                return status;
            }
        }

        /// <summary>
        /// Progresso de preenchimento da proposta (0-100)
        /// </summary>
        public int ProgressoPreenchimento
        {
            get
            {
                var pontos = 0;
                var totalPontos = 7;

                if (!string.IsNullOrEmpty(Titulo)) pontos++;
                if (NumeroPassageiros > 0) pontos++;
                if (DataInicio.HasValue) pontos++;
                if (DataFim.HasValue) pontos++;
                if (TemFotoCapa) pontos++;
                if (TemObservacoes) pontos++;
                if (LinkPublicoAtivo) pontos++;

                return (int)Math.Round((double)pontos / totalPontos * 100);
            }
        }

        /// <summary>
        /// Descrição textual do progresso
        /// </summary>
        public string DescricaoProgresso
        {
            get
            {
                var progresso = ProgressoPreenchimento;
                return progresso switch
                {
                    >= 90 => "Proposta quase completa",
                    >= 70 => "Boa quantidade de informações",
                    >= 50 => "Informações básicas preenchidas",
                    >= 30 => "Poucas informações preenchidas",
                    _ => "Proposta incompleta"
                };
            }
        }

        /// <summary>
        /// Classe CSS para barra de progresso
        /// </summary>
        public string ClasseProgresso
        {
            get
            {
                var progresso = ProgressoPreenchimento;
                return progresso switch
                {
                    >= 80 => "bg-success",
                    >= 60 => "bg-info",
                    >= 40 => "bg-warning",
                    _ => "bg-danger"
                };
            }
        }

        // === MÉTODOS AUXILIARES ===

        /// <summary>
        /// Limpa campos opcionais vazios
        /// </summary>
        public void LimparCamposVazios()
        {
            if (string.IsNullOrWhiteSpace(ObservacoesGerais))
                ObservacoesGerais = null;

            if (string.IsNullOrWhiteSpace(FotoCapa))
                FotoCapa = null;
        }

        /// <summary>
        /// Aplica valores padrão para nova proposta
        /// </summary>
        public void AplicarValoresPadrao()
        {
            if (IsNovaProposta)
            {
                StatusProposta = StatusProposta.Rascunho;
                LinkPublicoAtivo = true;
                NumeroPassageiros = 1;
                NumeroCriancas = 0;
                DataCriacao = DateTime.Now;
            }
        }

        /// <summary>
        /// Valida se upload de arquivo é permitido
        /// </summary>
        /// <returns>True se permitido, False caso contrário</returns>
        public bool ValidarUploadFoto()
        {
            if (FotoCapaUpload == null)
                return true;

            // Validar tamanho (5MB)
            if (FotoCapaUpload.Length > 5 * 1024 * 1024)
                return false;

            // Validar extensão
            var extensao = Path.GetExtension(FotoCapaUpload.FileName).ToLowerInvariant();
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

            return extensoesPermitidas.Contains(extensao);
        }

        /// <summary>
        /// Obtém mensagem de erro para upload inválido
        /// </summary>
        /// <returns>Mensagem de erro ou null se válido</returns>
        public string? ObterErroUploadFoto()
        {
            if (FotoCapaUpload == null)
                return null;

            if (FotoCapaUpload.Length > 5 * 1024 * 1024)
                return "Arquivo muito grande. Máximo 5MB permitido";

            var extensao = Path.GetExtension(FotoCapaUpload.FileName).ToLowerInvariant();
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

            if (!extensoesPermitidas.Contains(extensao))
                return "Formato não permitido. Use JPG, PNG, GIF, BMP ou WebP";

            return null;
        }

        /// <summary>
        /// Converte para string para debug
        /// </summary>
        /// <returns>Representação string do objeto</returns>
        public override string ToString()
        {
            return $"Proposta: {Titulo} - Status: {StatusPropostaTexto} - Passageiros: {TotalPessoas}";
        }
    }
}