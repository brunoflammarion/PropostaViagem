using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public enum TipoVoo
    {
        Ida = 1,
        Interno = 3,
        Volta = 2
    }

    public class Voo
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PropostaId { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Número do Voo")]
        public string NumeroVoo { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tipo")]
        public TipoVoo TipoVoo { get; set; } = TipoVoo.Ida;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Companhia")]
        public string Companhia { get; set; } = string.Empty;

        [MaxLength(50)]
        [Display(Name = "Classe")]
        public string? Classe { get; set; }

        [MaxLength(20)]
        [Display(Name = "Duração")]
        public string? Duracao { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Origem")]
        public string Origem { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Display(Name = "Destino")]
        public string Destino { get; set; } = string.Empty;

        [Display(Name = "Horário de Saída")]
        public DateTime? HorarioSaida { get; set; }

        [Display(Name = "Horário de Chegada")]
        public DateTime? HorarioChegada { get; set; }

        // ── Bagagem permitida ─────────────────────────────────────────────────────
        // Item pessoal (bolsa, mochila etc.)
        [MaxLength(200)]
        [Display(Name = "Item Pessoal — Descrição")]
        public string? BagagemItemPessoalDescricao { get; set; }

        [MaxLength(100)]
        [Display(Name = "Item Pessoal — Medidas")]
        public string? BagagemItemPessoalMedidas { get; set; }

        // Bagagem de mão
        [Display(Name = "Bagagem de Mão — Peso (kg)")]
        public decimal? BagagemMaoPeso { get; set; }

        [MaxLength(100)]
        [Display(Name = "Bagagem de Mão — Medidas")]
        public string? BagagemMaoMedidas { get; set; }

        // Bagagem despachada
        [Display(Name = "Bagagem Despachada — Peso (kg)")]
        public decimal? BagagemDespachadaPeso { get; set; }

        [MaxLength(100)]
        [Display(Name = "Bagagem Despachada — Medidas")]
        public string? BagagemDespachadaMedidas { get; set; }

        [MaxLength(4000)]
        [Display(Name = "Observação")]
        public string? Observacao { get; set; }

        [MaxLength(500)]
        [Display(Name = "Imagem da Observação")]
        public string? ObservacaoImagemPath { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual Proposta Proposta { get; set; } = null!;
        public virtual ICollection<PassageiroVoo> Passageiros { get; set; } = new List<PassageiroVoo>();
        public virtual ICollection<VooAnexo> Anexos { get; set; } = new List<VooAnexo>();
    }
}
