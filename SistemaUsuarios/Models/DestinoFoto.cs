using System.ComponentModel.DataAnnotations;

namespace SistemaUsuarios.Models
{
    public class DestinoFoto
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // ✅ Mudança: Guid em vez de int

        [Required]
        public Guid DestinoId { get; set; } // ✅ FK para Destino (Guid)

        [Required]
        [MaxLength(500)]
        public string CaminhoFoto { get; set; }

        [MaxLength(200)]
        public string? Descricao { get; set; }

        public int Ordem { get; set; } = 1;

        public bool Principal { get; set; } = false;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // ✅ NAVEGAÇÃO PARA DESTINO (N:1)
        public virtual Destino Destino { get; set; }
    }
}