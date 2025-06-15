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

        // ✅ NOVOS DBSETS
        public DbSet<Destino> Destinos { get; set; }
        public DbSet<DestinoFoto> DestinoFotos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ CONFIGURAÇÃO DA TABELA USUARIOS
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ✅ CONFIGURAÇÃO DA TABELA PROPOSTAS
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Proposta>()
                .Property(p => p.StatusProposta)
                .HasConversion<int>();

            // ✅ CONFIGURAÇÃO DA TABELA DESTINOS
            modelBuilder.Entity<Destino>()
                .HasOne(d => d.Proposta)
                .WithMany(p => p.Destinos)
                .HasForeignKey(d => d.PropostaId)
                .OnDelete(DeleteBehavior.Cascade); // Quando a proposta for deletada, os destinos também serão

            modelBuilder.Entity<Destino>()
                .HasIndex(d => d.PropostaId);

            modelBuilder.Entity<Destino>()
                .HasIndex(d => new { d.PropostaId, d.Ordem })
                .IsUnique(); // Garante que não haja duas ordens iguais na mesma proposta

            // ✅ CONFIGURAÇÃO DA TABELA DESTINO_FOTOS
            modelBuilder.Entity<DestinoFoto>()
                .HasOne(df => df.Destino)
                .WithMany(d => d.Fotos)
                .HasForeignKey(df => df.DestinoId)
                .OnDelete(DeleteBehavior.Cascade); // Quando o destino for deletado, as fotos também serão

            modelBuilder.Entity<DestinoFoto>()
                .HasIndex(df => df.DestinoId);

            modelBuilder.Entity<DestinoFoto>()
                .HasIndex(df => new { df.DestinoId, df.Ordem })
                .IsUnique(); // Garante que não haja duas ordens iguais no mesmo destino

            // ✅ CONFIGURAÇÃO DA TABELA PROPOSTA_VISUALIZACAO
            modelBuilder.Entity<PropostaVisualizacao>()
                .HasOne(pv => pv.Proposta)
                .WithMany(p => p.PropostaVisualizacoes)
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

            // ✅ CONFIGURAÇÕES DE PRECISÃO PARA CAMPOS DECIMAIS
            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.Latitude)
                .HasPrecision(10, 8);

            modelBuilder.Entity<PropostaVisualizacao>()
                .Property(pv => pv.Longitude)
                .HasPrecision(11, 8);

            // ✅ CONFIGURAÇÃO DE VALORES PADRÃO
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