using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Processo> Processos => Set<Processo>();
    public DbSet<Criterio> Criterios => Set<Criterio>();
    public DbSet<Proposta> Propostas => Set<Proposta>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<ItemMaterial> ItensMaterial => Set<ItemMaterial>();
    public DbSet<ItemProposta> ItensProposta => Set<ItemProposta>();
    public DbSet<PropostaAnexo> PropostasAnexo => Set<PropostaAnexo>();
    public DbSet<PedidoProposta> Pedidos => Set<PedidoProposta>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

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

            // Restrict (not Cascade/SetNull): SQL Server rejects SetNull here too, since
            // Proposta is already reachable from Processo via a Cascade path (ProcessoId),
            // and a second actionable path through PedidoProposta creates the same
            // multiple-cascade-paths conflict seen with Avaliacao->Criterio.
            // PedidosController.Delete unlinks any Propostas before removing the Pedido.
            e.HasOne(p => p.PedidoProposta)
                .WithMany(pp => pp.Propostas)
                .HasForeignKey(p => p.PedidoPropostaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PedidoProposta>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(p => p.Processo)
                .WithMany(pr => pr.Pedidos)
                .HasForeignKey(p => p.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemPedido>(e =>
        {
            e.Property(p => p.QuantidadeSolicitada).HasPrecision(18, 3);

            e.HasOne(ip => ip.PedidoProposta)
                .WithMany(p => p.ItensPedido)
                .HasForeignKey(ip => ip.PedidoPropostaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ip => ip.ItemMaterial)
                .WithMany()
                .HasForeignKey(ip => ip.ItemMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Criterio>(e =>
        {
            e.Property(c => c.Peso).HasPrecision(5, 2);

            e.HasOne(c => c.Processo)
                .WithMany(p => p.Criterios)
                .HasForeignKey(c => c.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade);
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
            // Removing a single Criterio while Avaliacoes still reference it must be handled
            // explicitly in application code (delete the Avaliacoes first).
            e.HasOne(a => a.Criterio)
                .WithMany(c => c.Avaliacoes)
                .HasForeignKey(a => a.CriterioId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(a => new { a.PropostaId, a.CriterioId }).IsUnique();
        });

        modelBuilder.Entity<ItemMaterial>(e =>
        {
            e.HasOne(im => im.ItemPai)
                .WithMany(im => im.SubItens)
                .HasForeignKey(im => im.ItemPaiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemProposta>(e =>
        {
            e.Property(p => p.Quantidade).HasPrecision(18, 3);
            e.Property(p => p.QuantidadeSolicitada).HasPrecision(18, 3);
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
