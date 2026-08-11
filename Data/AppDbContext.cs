using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Processo> Processos => Set<Processo>();
    public DbSet<CriterioAvaliacao> CriteriosAvaliacao => Set<CriterioAvaliacao>();
    public DbSet<ProcessoCriterio> ProcessosCriterio => Set<ProcessoCriterio>();
    public DbSet<Proposta> Propostas => Set<Proposta>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<ItemMaterial> ItensMaterial => Set<ItemMaterial>();
    public DbSet<ItemProposta> ItensProposta => Set<ItemProposta>();
    public DbSet<PropostaAnexo> PropostasAnexo => Set<PropostaAnexo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Processo>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.OrcamentoEstimado).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Proposta>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.ValorTotal).HasPrecision(18, 2);

            e.HasOne(p => p.Processo)
                .WithMany(pr => pr.Propostas)
                .HasForeignKey(p => p.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessoCriterio>(e =>
        {
            e.Property(p => p.Peso).HasPrecision(5, 2);

            e.HasOne(pc => pc.Processo)
                .WithMany(p => p.Criterios)
                .HasForeignKey(pc => pc.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pc => pc.CriterioAvaliacao)
                .WithMany(c => c.ProcessosCriterio)
                .HasForeignKey(pc => pc.CriterioAvaliacaoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(pc => new { pc.ProcessoId, pc.CriterioAvaliacaoId }).IsUnique();
        });

        modelBuilder.Entity<Avaliacao>(e =>
        {
            e.Property(a => a.Nota).HasPrecision(5, 2);

            e.HasOne(a => a.Proposta)
                .WithMany(p => p.Avaliacoes)
                .HasForeignKey(a => a.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not Cascade) to avoid SQL Server's multiple-cascade-paths error:
            // Avaliacao already cascades from Proposta, which itself cascades from Processo,
            // so deleting a Processo/Proposta already removes its Avaliacoes via that path.
            // Removing a single ProcessoCriterio while Avaliacoes still reference it must be
            // handled explicitly in application code (delete the Avaliacoes first).
            e.HasOne(a => a.ProcessoCriterio)
                .WithMany(pc => pc.Avaliacoes)
                .HasForeignKey(a => a.ProcessoCriterioId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(a => new { a.PropostaId, a.ProcessoCriterioId }).IsUnique();
        });

        modelBuilder.Entity<ItemProposta>(e =>
        {
            e.Property(p => p.Quantidade).HasPrecision(18, 3);
            e.Property(p => p.PrecoUnitario).HasPrecision(18, 2);

            e.HasOne(ip => ip.Proposta)
                .WithMany(p => p.ItensProposta)
                .HasForeignKey(ip => ip.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ip => ip.ItemMaterial)
                .WithMany(im => im.ItensProposta)
                .HasForeignKey(ip => ip.ItemMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PropostaAnexo>(e =>
        {
            e.HasOne(a => a.Proposta)
                .WithMany(p => p.Anexos)
                .HasForeignKey(a => a.PropostaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
