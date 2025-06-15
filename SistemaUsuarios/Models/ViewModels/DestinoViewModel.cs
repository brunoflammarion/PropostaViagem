using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models.ViewModels
{
    public class DestinoViewModel
    {
        public int Id { get; set; }

        public int PropostaId { get; set; }

        [Required(ErrorMessage = "Nome do destino é obrigatório")]
        [MaxLength(200, ErrorMessage = "Nome deve ter no máximo 200 caracteres")]
        public string Nome { get; set; }

        [MaxLength(1000, ErrorMessage = "Descrição deve ter no máximo 1000 caracteres")]
        public string? Descricao { get; set; }

        public DateTime? DataChegada { get; set; }

        public DateTime? DataSaida { get; set; }

        public int Ordem { get; set; }

        [MaxLength(100)]
        public string? Pais { get; set; }

        [MaxLength(100)]
        public string? Cidade { get; set; }

        public DateTime DataCriacao { get; set; }
    }
}