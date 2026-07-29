namespace SistemaUsuarios.Models
{
    public class PasswordResetToken
    {
        public Guid     Id        { get; set; } = Guid.NewGuid();
        public Guid     UsuarioId { get; set; }
        public string   Token     { get; set; } = "";
        public DateTime Expiry    { get; set; }
        public bool     Usado     { get; set; } = false;
        public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;

        public virtual Usuario Usuario { get; set; } = null!;
    }
}
