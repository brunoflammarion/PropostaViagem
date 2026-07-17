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
        public DbSet<Hospedagem> Hospedagens { get; set; }
        public DbSet<HospedagemComodidade> HospedagemComodidades { get; set; }
        public DbSet<HospedagemFoto> HospedagemFotos { get; set; }
        public DbSet<Acomodacao> Acomodacoes { get; set; }
        public DbSet<AcomodacaoFoto> AcomodacaoFotos { get; set; }
        public DbSet<AcomodacaoComodidade> AcomodacaoComodidades { get; set; }
        public DbSet<Voo> Voos { get; set; }
        public DbSet<PassageiroVoo> PassageirosVoo { get; set; }
        public DbSet<VooAnexo> VooAnexos { get; set; }
        public DbSet<Experiencia> Experiencias { get; set; }
        public DbSet<ExperienciaImagem> ExperienciaImagens { get; set; }
        public DbSet<ExperienciaArquivo> ExperienciaArquivos { get; set; }
        public DbSet<Seguro> Seguros { get; set; }
        public DbSet<SeguroImagem> SeguroImagens { get; set; }
        public DbSet<SeguroDocumento> SeguroDocumentos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<PassageiroProposta> PassageirosProposta { get; set; }
        public DbSet<Transporte> Transportes { get; set; }
        public DbSet<TransporteImagem> TransporteImagens { get; set; }
        public DbSet<TransporteDocumento> TransporteDocumentos { get; set; }
        public DbSet<AvaliacaoCliente> AvaliacoesCliente { get; set; }

        // ── MÓDULO OFERTAS ─────────────────────────────────────────────────────────
        public DbSet<Oferta> Ofertas { get; set; }

        // ── MÓDULO CAPTAÇÃO ────────────────────────────────────────────────────────
        public DbSet<LeadCaptureSettings> LeadCaptureSettings { get; set; }
        public DbSet<Lead> Leads { get; set; }

        // ── MÓDULO TAREFAS ─────────────────────────────────────────────────────
        public DbSet<Tarefa> Tarefas { get; set; }
        public DbSet<ConfiguracaoLembrete> ConfiguracoesLembrete { get; set; }

        // ── IMPORTAÇÃO IA ──────────────────────────────────────────────────────
        public DbSet<ImportacaoSessao> ImportacaoSessoes { get; set; }

        // ── PLATFORM ADMIN ─────────────────────────────────────────────────────
        public DbSet<AdminPlataforma> AdminsPlataforma { get; set; }

        // ── CONTEÚDOS DE DEMONSTRAÇÃO ───────────────────────────────────────
        public DbSet<ConteudoDemonstracao> ConteudosDemonstracao { get; set; }
        public DbSet<ConteudoDemonstracaoAplicado> ConteudosDemonstracaoAplicados { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ CONFIGURAÇÃO DA TABELA USUARIOS
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.SlugAgencia)
                .IsUnique()
                .HasFilter("[SlugAgencia] IS NOT NULL");

            modelBuilder.Entity<Usuario>()
                .Property(u => u.TipoUsuario)
                .HasConversion<int>()
                .HasDefaultValue(TipoUsuario.Master);

            // Auto-relacionamento Master → Associados
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.UsuarioMaster)
                .WithMany(u => u.Associados)
                .HasForeignKey(u => u.UsuarioMasterId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            // ✅ CONFIGURAÇÃO DA TABELA PROPOSTAS
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Proposta → UsuarioMaster (FK nullable, sem cascade para evitar múltiplos caminhos)
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.UsuarioMaster)
                .WithMany()
                .HasForeignKey(p => p.UsuarioMasterId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            // Proposta → UsuarioResponsavel (FK nullable — muda quando master transfere a proposta)
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.UsuarioResponsavel)
                .WithMany()
                .HasForeignKey(p => p.UsuarioResponsavelId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

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

            modelBuilder.Entity<Destino>()
                .Property(d => d.Localizacao)
                .HasColumnType("geography");

            // CONFIGURAÇÃO DA TABELA HOSPEDAGENS
            modelBuilder.Entity<Hospedagem>()
                .HasOne(h => h.Destino)
                .WithMany(d => d.Hospedagens)
                .HasForeignKey(h => h.DestinoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Hospedagem>()
                .HasIndex(h => h.DestinoId);

            modelBuilder.Entity<Hospedagem>()
                .Property(h => h.Categoria)
                .HasConversion<int>();

            modelBuilder.Entity<Hospedagem>()
                .Property(h => h.TipoPensao)
                .HasConversion<int>();

            // CONFIGURAÇÃO DA TABELA HOSPEDAGEM_COMODIDADES
            modelBuilder.Entity<HospedagemComodidade>()
                .HasOne(c => c.Hospedagem)
                .WithMany(h => h.Comodidades)
                .HasForeignKey(c => c.HospedagemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HospedagemComodidade>()
                .HasIndex(c => c.HospedagemId);

            // CONFIGURAÇÃO DA TABELA ACOMODACOES
            modelBuilder.Entity<Acomodacao>()
                .HasOne(a => a.Hospedagem)
                .WithMany(h => h.Acomodacoes)
                .HasForeignKey(a => a.HospedagemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Acomodacao>()
                .HasIndex(a => a.HospedagemId);

            // CONFIGURAÇÃO DA TABELA HOSPEDAGEM_FOTOS
            modelBuilder.Entity<HospedagemFoto>()
                .HasOne(hf => hf.Hospedagem)
                .WithMany(h => h.Fotos)
                .HasForeignKey(hf => hf.HospedagemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HospedagemFoto>()
                .HasIndex(hf => hf.HospedagemId);

            // CONFIGURAÇÃO DA TABELA ACOMODACAO_FOTOS
            modelBuilder.Entity<AcomodacaoFoto>()
                .HasOne(af => af.Acomodacao)
                .WithMany(a => a.Fotos)
                .HasForeignKey(af => af.AcomodacaoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AcomodacaoFoto>()
                .HasIndex(af => af.AcomodacaoId);

            modelBuilder.Entity<AcomodacaoFoto>()
                .HasIndex(af => new { af.AcomodacaoId, af.Ordem })
                .IsUnique();

            // CONFIGURAÇÃO DA TABELA ACOMODACAO_COMODIDADES
            modelBuilder.Entity<AcomodacaoComodidade>()
                .HasOne(ac => ac.Acomodacao)
                .WithMany(a => a.Comodidades)
                .HasForeignKey(ac => ac.AcomodacaoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AcomodacaoComodidade>()
                .HasIndex(ac => ac.AcomodacaoId);

            // CONFIGURAÇÃO DA TABELA VOOS
            modelBuilder.Entity<Voo>()
                .HasOne(v => v.Proposta)
                .WithMany(p => p.Voos)
                .HasForeignKey(v => v.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Voo>()
                .HasIndex(v => v.PropostaId);

            modelBuilder.Entity<Voo>()
                .Property(v => v.TipoVoo)
                .HasConversion<int>();

            // CONFIGURAÇÃO DA TABELA PASSAGEIROS_VOO
            modelBuilder.Entity<PassageiroVoo>()
                .HasOne(p => p.Voo)
                .WithMany(v => v.Passageiros)
                .HasForeignKey(p => p.VooId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PassageiroVoo>()
                .HasIndex(p => p.VooId);

            // CONFIGURAÇÃO DA TABELA VOO_ANEXOS
            modelBuilder.Entity<VooAnexo>()
                .HasOne(a => a.Voo)
                .WithMany(v => v.Anexos)
                .HasForeignKey(a => a.VooId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VooAnexo>()
                .HasIndex(a => a.VooId);

            // CONFIGURAÇÃO DA TABELA EXPERIENCIAS
            modelBuilder.Entity<Experiencia>()
                .HasOne(e => e.Destino)
                .WithMany(d => d.Experiencias)
                .HasForeignKey(e => e.DestinoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Experiencia>()
                .HasIndex(e => e.DestinoId);

            modelBuilder.Entity<Experiencia>()
                .Property(e => e.Valor)
                .HasColumnType("decimal(18,2)");

            // CONFIGURAÇÃO DA TABELA EXPERIENCIA_IMAGENS
            modelBuilder.Entity<ExperienciaImagem>()
                .HasOne(i => i.Experiencia)
                .WithMany(e => e.Imagens)
                .HasForeignKey(i => i.ExperienciaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExperienciaImagem>()
                .HasIndex(i => i.ExperienciaId);

            // CONFIGURAÇÃO DA TABELA EXPERIENCIA_ARQUIVOS
            modelBuilder.Entity<ExperienciaArquivo>()
                .HasOne(a => a.Experiencia)
                .WithMany(e => e.Arquivos)
                .HasForeignKey(a => a.ExperienciaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExperienciaArquivo>()
                .HasIndex(a => a.ExperienciaId);

            // CONFIGURAÇÃO DA TABELA SEGUROS
            modelBuilder.Entity<Seguro>()
                .HasOne(s => s.Proposta)
                .WithMany(p => p.Seguros)
                .HasForeignKey(s => s.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Seguro>()
                .HasIndex(s => s.PropostaId);

            modelBuilder.Entity<Seguro>()
                .Property(s => s.Valor)
                .HasColumnType("decimal(18,2)");

            // CONFIGURAÇÃO DA TABELA SEGURO_IMAGENS
            modelBuilder.Entity<SeguroImagem>()
                .HasOne(i => i.Seguro)
                .WithMany(s => s.Imagens)
                .HasForeignKey(i => i.SeguroId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SeguroImagem>()
                .HasIndex(i => i.SeguroId);

            // CONFIGURAÇÃO DA TABELA SEGURO_DOCUMENTOS
            modelBuilder.Entity<SeguroDocumento>()
                .HasOne(d => d.Seguro)
                .WithMany(s => s.Documentos)
                .HasForeignKey(d => d.SeguroId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SeguroDocumento>()
                .HasIndex(d => d.SeguroId);

            // CONFIGURAÇÃO DA TABELA CLIENTES (entidade própria — 1 cliente : N propostas)
            modelBuilder.Entity<Cliente>()
                .HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cliente>()
                .HasIndex(c => c.UsuarioId);

            modelBuilder.Entity<Cliente>()
                .HasIndex(c => new { c.UsuarioId, c.IsDeleted });

            modelBuilder.Entity<Cliente>()
                .Property(c => c.Genero)
                .HasConversion<int?>();

            // Auto-referência: cliente indicador (sem cascade para evitar ciclo)
            modelBuilder.Entity<Cliente>()
                .HasOne(c => c.ClienteIndicador)
                .WithMany()
                .HasForeignKey(c => c.ClienteIndicadorId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            modelBuilder.Entity<Cliente>()
                .HasIndex(c => c.ClienteIndicadorId)
                .HasFilter("[ClienteIndicadorId] IS NOT NULL");

            // Proposta → Cliente (N:1, nullable)
            modelBuilder.Entity<Proposta>()
                .HasOne(p => p.Cliente)
                .WithMany(c => c.Propostas)
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // CONFIGURAÇÃO DA TABELA PASSAGEIROS_PROPOSTA (1:N)
            modelBuilder.Entity<PassageiroProposta>()
                .HasOne(p => p.Proposta)
                .WithMany(pr => pr.PassageirosProposta)
                .HasForeignKey(p => p.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PassageiroProposta>()
                .HasIndex(p => p.PropostaId);

            modelBuilder.Entity<PassageiroProposta>()
                .Property(p => p.Genero)
                .HasConversion<int?>();

            modelBuilder.Entity<PassageiroProposta>()
                .Property(p => p.Relacionamento)
                .HasConversion<int?>();

            // CONFIGURAÇÃO DA TABELA TRANSPORTES
            modelBuilder.Entity<Transporte>()
                .HasOne(t => t.Destino)
                .WithMany(d => d.Transportes)
                .HasForeignKey(t => t.DestinoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Transporte>()
                .HasIndex(t => t.DestinoId);

            modelBuilder.Entity<Transporte>()
                .Property(t => t.Valor)
                .HasColumnType("decimal(18,2)");

            // CONFIGURAÇÃO DA TABELA TRANSPORTE_IMAGENS
            modelBuilder.Entity<TransporteImagem>()
                .HasOne(i => i.Transporte)
                .WithMany(t => t.Imagens)
                .HasForeignKey(i => i.TransporteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TransporteImagem>()
                .HasIndex(i => i.TransporteId);

            // CONFIGURAÇÃO DA TABELA TRANSPORTE_DOCUMENTOS
            modelBuilder.Entity<TransporteDocumento>()
                .HasOne(d => d.Transporte)
                .WithMany(t => t.Documentos)
                .HasForeignKey(d => d.TransporteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TransporteDocumento>()
                .HasIndex(d => d.TransporteId);

            // AvaliacaoCliente
            modelBuilder.Entity<AvaliacaoCliente>()
                .HasOne(a => a.Proposta)
                .WithMany()
                .HasForeignKey(a => a.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AvaliacaoCliente>()
                .Property(a => a.TipoItem)
                .HasConversion<int>();

            modelBuilder.Entity<AvaliacaoCliente>()
                .HasIndex(a => new { a.PropostaId, a.TipoItem, a.ItemId });

            // ── MÓDULO OFERTAS ─────────────────────────────────────────────────────
            // Oferta → UsuarioCriador (imutável, Restrict para preservar histórico)
            modelBuilder.Entity<Oferta>()
                .HasOne(o => o.Usuario)
                .WithMany()
                .HasForeignKey(o => o.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Oferta → UsuarioMaster (nullable, NoAction para evitar múltiplos caminhos de cascade)
            modelBuilder.Entity<Oferta>()
                .HasOne(o => o.UsuarioMaster)
                .WithMany()
                .HasForeignKey(o => o.UsuarioMasterId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            modelBuilder.Entity<Oferta>()
                .HasIndex(o => o.UsuarioId);

            modelBuilder.Entity<Oferta>()
                .HasIndex(o => o.UsuarioMasterId);

            // ── MÓDULO CAPTAÇÃO ───────────────────────────────────────────────────
            modelBuilder.Entity<LeadCaptureSettings>()
                .HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeadCaptureSettings>()
                .HasIndex(s => s.UsuarioId)
                .IsUnique(); // 1 settings por usuário

            modelBuilder.Entity<Lead>()
                .HasOne(l => l.Usuario)
                .WithMany()
                .HasForeignKey(l => l.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Lead>()
                .HasIndex(l => l.UsuarioId);

            modelBuilder.Entity<Lead>()
                .HasIndex(l => l.CreatedAt);

            modelBuilder.Entity<Lead>()
                .Property(l => l.Status)
                .HasConversion<int>();

            // ── MÓDULO TAREFAS ──────────────────────────────────────────────────
            modelBuilder.Entity<Tarefa>()
                .HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tarefa>()
                .HasOne(t => t.Cliente)
                .WithMany()
                .HasForeignKey(t => t.ClienteId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            modelBuilder.Entity<Tarefa>()
                .HasOne(t => t.Proposta)
                .WithMany()
                .HasForeignKey(t => t.PropostaId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            modelBuilder.Entity<Tarefa>()
                .HasIndex(t => t.UsuarioId);

            modelBuilder.Entity<Tarefa>()
                .HasIndex(t => new { t.UsuarioId, t.Status, t.IsDeleted });

            modelBuilder.Entity<Tarefa>()
                .HasIndex(t => new { t.UsuarioId, t.DataVencimento, t.IsDeleted });

            modelBuilder.Entity<Tarefa>()
                .HasIndex(t => new { t.PropostaId, t.TemplateCodigo, t.IsDeleted })
                .HasFilter("[PropostaId] IS NOT NULL AND [TemplateCodigo] IS NOT NULL");

            modelBuilder.Entity<ConfiguracaoLembrete>()
                .HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConfiguracaoLembrete>()
                .HasIndex(c => new { c.UsuarioId, c.TemplateCodigo })
                .IsUnique();

            // ── PLATFORM ADMIN ────────────────────────────────────────────────
            modelBuilder.Entity<AdminPlataforma>()
                .HasIndex(a => a.Email)
                .IsUnique();

            // ── CONTEÚDOS DE DEMONSTRAÇÃO ─────────────────────────────────────
            modelBuilder.Entity<ConteudoDemonstracao>()
                .Property(c => c.TipoConteudo)
                .HasConversion<int>();

            modelBuilder.Entity<ConteudoDemonstracao>()
                .HasOne(c => c.CriadoPorAdmin)
                .WithMany()
                .HasForeignKey(c => c.CriadoPorAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConteudoDemonstracao>()
                .HasOne(c => c.AtualizadoPorAdmin)
                .WithMany()
                .HasForeignKey(c => c.AtualizadoPorAdminId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            modelBuilder.Entity<ConteudoDemonstracaoAplicado>()
                .HasOne(a => a.ConteudoDemonstracao)
                .WithMany(c => c.Aplicacoes)
                .HasForeignKey(a => a.ConteudoDemonstracaoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConteudoDemonstracaoAplicado>()
                .Property(a => a.StatusAplicacao)
                .HasConversion<int>();

            // Índice único: cada conteúdo só pode ser aplicado uma vez por agência (idempotência)
            modelBuilder.Entity<ConteudoDemonstracaoAplicado>()
                .HasIndex(a => new { a.ConteudoDemonstracaoId, a.AgenciaMasterId })
                .IsUnique();
        }
    }
}