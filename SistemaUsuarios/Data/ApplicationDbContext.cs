using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Models;

namespace SistemaUsuarios.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Proposta> Propostas { get; set; }
        public DbSet<PropostaVisualizacao> PropostaVisualizacoes { get; set; }

        public DbSet<Layout> Layouts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da tabela Usuarios
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configuração da tabela Propostas
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Proposta>()
                .Property(p => p.StatusProposta)
                .HasConversion<int>();

            // Configuração da tabela PropostaVisualizacao
            modelBuilder.Entity<PropostaVisualizacao>()
                .HasOne(pv => pv.Proposta)
                .WithMany()
                .HasForeignKey(pv => pv.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PropostaVisualizacao>()
                .HasIndex(pv => pv.SessionToken);

            modelBuilder.Entity<PropostaVisualizacao>()
                .HasIndex(pv => pv.DataHoraInicio);

            modelBuilder.Entity<PropostaVisualizacao>()
                .HasIndex(pv => pv.PropostaId);

            modelBuilder.Entity<PropostaVisualizacao>()
                .HasIndex(pv => pv.DeviceFingerprint);

            // Configurações de precisão para campos decimais
            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.Latitude)
                .HasPrecision(10, 8);

            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.Longitude)
                .HasPrecision(11, 8);

            // Configuração de valores padrão
            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.DataCriacao)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.ClicouWhatsApp)
                .HasDefaultValue(false);

            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.ClicouEmail)
                .HasDefaultValue(false);

            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.NumeroCliques)
                .HasDefaultValue(0);
        }
    }
}