using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public enum RelacionamentoPassageiro
    {
        Filho = 1,
        Filha = 2,
        Amigo = 3,
        Amiga = 4,
        Esposa = 5,
        Marido = 6,
        Sogra = 7,
        Sogro = 8,
        Cunhado = 9,
        Cunhada = 10,
        Neto = 11,
        Neta = 12,
        Pai = 13,
        Mae = 14,
        Irmao = 15,
        Irma = 16,
        Outro = 99
    }

    public enum FaixaEtariaPassageiro
    {
        Bebe = 1,
        Crianca = 2,
        Adolescente = 3,
        Adulto = 4
    }

    public enum ModoBebe
    {
        Colo = 1,
        AssentoProprio = 2
    }

    public class PassageiroProposta
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PropostaId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        public DateTime? DataNascimento { get; set; }

        public Genero? Genero { get; set; }

        public RelacionamentoPassageiro? Relacionamento { get; set; }

        [MaxLength(2000)]
        public string? Observacoes { get; set; }

        public int Ordem { get; set; } = 1;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Vínculo opcional com cliente cadastrado
        public Guid? ClienteId { get; set; }
        public virtual Cliente? Cliente { get; set; }

        // Faixa etária aproximada (quando data de nascimento não informada)
        public FaixaEtariaPassageiro? FaixaEtaria { get; set; }
        public bool FaixaIsAproximada { get; set; } = false;

        // Bebê: modo de viagem
        public ModoBebe? ModoBebe { get; set; }

        // Responsável durante a viagem (para menores desacompanhados)
        public Guid? ResponsavelId { get; set; }
        public virtual PassageiroProposta? Responsavel { get; set; }

        // Navigation
        public virtual Proposta Proposta { get; set; }

        // Computed (usa DateTime.Today; para cálculo na data da viagem use IPassengerAgeService)
        public int? IdadeCalculada => DataNascimento.HasValue
            ? CalcularIdade(DataNascimento.Value)
            : null;

        private static int CalcularIdade(DateTime nascimento)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - nascimento.Year;
            if (nascimento.Month > hoje.Month ||
                (nascimento.Month == hoje.Month && nascimento.Day > hoje.Day))
                idade--;
            return idade;
        }
    }
}
