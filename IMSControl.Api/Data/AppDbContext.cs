using IMSControl.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IMSControl.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductRecipeItem> ProductRecipeItems => Set<ProductRecipeItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Production> Productions => Set<Production>();
    public DbSet<ProductionSupplyUsed> ProductionSuppliesUsed => Set<ProductionSupplyUsed>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Recipe)
            .WithOne(r => r.Product)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductRecipeItem>()
            .HasOne<Supply>()
            .WithMany()
            .HasForeignKey(r => r.SupplyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Production>()
            .HasMany(p => p.SuppliesUsed)
            .WithOne(s => s.Production)
            .HasForeignKey(s => s.ProductionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
