using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public enum CategoriaHospedagem
    {
        [Display(Name = "Hotel")]
        Hotel = 1,
        [Display(Name = "Pousada")]
        Pousada = 2,
        [Display(Name = "Resort")]
        Resort = 3,
        [Display(Name = "Hotel fazenda")]
        HotelFazenda = 4,
        [Display(Name = "Aluguel de casa")]
        AluguelCasa = 5,
        [Display(Name = "Camping")]
        Camping = 6,
        [Display(Name = "Outros")]
        Outros = 7
    }

    public enum TipoPensao
    {
        [Display(Name = "Sem refeições")]
        SemPensao = 1,
        [Display(Name = "Café da manhã")]
        CafeDaManha = 2,
        [Display(Name = "Meia pensão")]
        MeiaPensao = 3,
        [Display(Name = "Pensão completa")]
        PensaoCompleta = 4,
        [Display(Name = "All-inclusive")]
        AllInclusive = 5,
        [Display(Name = "Outros")]
        Outros = 6
    }

    public class Hospedagem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid DestinoId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        [MaxLength(4000)]
        public string? Descricao { get; set; }

        [MaxLength(500)]
        public string? Endereco { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }

        public CategoriaHospedagem Categoria { get; set; } = CategoriaHospedagem.Hotel;

        public int? NumeroEstrelas { get; set; }

        public TipoPensao TipoPensao { get; set; } = TipoPensao.SemPensao;

        [MaxLength(2000)]
        public string? Reserva { get; set; }

        [MaxLength(2000)]
        public string? Observacoes { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Navegação para Destino (N:1)
        public virtual Destino Destino { get; set; }

        // Navegação para Acomodacoes (1:N)
        public virtual ICollection<Acomodacao> Acomodacoes { get; set; } = new List<Acomodacao>();

        // Navegação para Comodidades do Hotel (1:N)
        public virtual ICollection<HospedagemComodidade> Comodidades { get; set; } = new List<HospedagemComodidade>();

        // Navegação para Fotos do Hotel (1:N)
        public virtual ICollection<HospedagemFoto> Fotos { get; set; } = new List<HospedagemFoto>();
    }
}
