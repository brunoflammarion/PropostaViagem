using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    /// <summary>
    /// ViewModel para criação e edição de Ofertas.
    /// Espelha os campos da entidade Oferta, servindo como contrato do formulário.
    /// </summary>
    public class OfertaViewModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "O nome da oferta é obrigatório.")]
        [MaxLength(200)]
        [Display(Name = "Nome da oferta")]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(50)]
        public string TemplateId { get; set; } = "destino";

        // ── Visuais ───────────────────────────────────────────────────────────────
        [MaxLength(20)]  public string? Cor1 { get; set; }
        [MaxLength(20)]  public string? Cor2 { get; set; }
        [MaxLength(20)]  public string? Cor3 { get; set; }
        [MaxLength(500)] public string? ImagemPrincipalPath { get; set; }
        [MaxLength(500)] public string? LogoPath { get; set; }
        [MaxLength(2000)] public string? SlotsJson { get; set; }

        // ── Bloco principal ───────────────────────────────────────────────────────
        [MaxLength(200)] public string? TituloPrincipal { get; set; }
        [MaxLength(200)] public string? Subtitulo { get; set; }
        [MaxLength(500)] public string? DescricaoCurta { get; set; }
        [MaxLength(500)] public string? TextoComplementar { get; set; }
        [MaxLength(300)] public string? Rodape { get; set; }
        [MaxLength(100)] public string? Cta { get; set; }

        // ── Bloco comercial ───────────────────────────────────────────────────────
        [MaxLength(100)] public string? Preco { get; set; }
        [MaxLength(100)] public string? PrecoAnterior { get; set; }
        [MaxLength(100)] public string? TextoAPartirDe { get; set; }
        [MaxLength(200)] public string? CondicaoEspecial { get; set; }
        [MaxLength(200)] public string? Parcelamento { get; set; }
        [MaxLength(200)] public string? TextoUrgencia { get; set; }
        [MaxLength(200)] public string? ValidadeOferta { get; set; }

        // ── Bloco produto ─────────────────────────────────────────────────────────
        [MaxLength(200)] public string? Destino { get; set; }
        [MaxLength(200)] public string? Origem { get; set; }
        [MaxLength(200)] public string? PeriodoViagem { get; set; }
        [MaxLength(50)]  public string? QtdNoites { get; set; }
        [MaxLength(200)] public string? CompanhiaAerea { get; set; }
        [MaxLength(200)] public string? Hotel { get; set; }
        [MaxLength(200)] public string? RegimeHospedagem { get; set; }
        [MaxLength(1000)] public string? InclusoesOferta { get; set; }
        [MaxLength(500)] public string? ObservacoesCurtas { get; set; }
        [MaxLength(500)] public string? RegrasCondicoes { get; set; }

        // ── Bloco contato ─────────────────────────────────────────────────────────
        [MaxLength(30)]  public string? WhatsApp { get; set; }
        [MaxLength(30)]  public string? Telefone { get; set; }
        [MaxLength(200)] public string? Email { get; set; }
        [MaxLength(100)] public string? Instagram { get; set; }
        [MaxLength(200)] public string? Site { get; set; }

        // ── Bloco institucional ───────────────────────────────────────────────────
        [MaxLength(200)] public string? NomeAgencia { get; set; }
        [MaxLength(200)] public string? SeloPromocional { get; set; }
        [MaxLength(100)] public string? TagPromocional { get; set; }
        [MaxLength(300)] public string? TextoInstitucional { get; set; }
    }
}
