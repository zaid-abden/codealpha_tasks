using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Models;

namespace RestaurantManagement.Api.Data;

public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<MenuItemIngredient> MenuItemIngredients => Set<MenuItemIngredient>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MenuItem
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(120);
            entity.Property(m => m.Description).HasMaxLength(500);
            entity.Property(m => m.Category).IsRequired().HasMaxLength(60);
            entity.Property(m => m.Price).HasPrecision(18, 2);
            entity.HasIndex(m => m.Category);
            entity.HasIndex(m => m.IsActive);
        });

        // RestaurantTable
        modelBuilder.Entity<RestaurantTable>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TableNumber).IsRequired();
            entity.HasIndex(t => t.TableNumber).IsUnique();
        });

        // Reservation
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.CustomerName).IsRequired().HasMaxLength(120);
            entity.Property(r => r.Email).HasMaxLength(150);
            entity.Property(r => r.Phone).HasMaxLength(30);

            entity.HasOne(r => r.Table)
                .WithMany(t => t.Reservations)
                .HasForeignKey(r => r.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.TableId, r.StartAtUtc, r.EndAtUtc });
            entity.HasIndex(r => r.Status);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.Property(o => o.Subtotal).HasPrecision(18, 2);
            entity.Property(o => o.Total).HasPrecision(18, 2);
            entity.Property(o => o.Notes).HasMaxLength(500);

            entity.HasOne(o => o.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(o => o.CreatedAtUtc);
            entity.HasIndex(o => o.Status);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.ItemNameSnapshot).IsRequired().HasMaxLength(120);
            entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            entity.Property(oi => oi.LineTotal).HasPrecision(18, 2);

            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.MenuItem)
                .WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InventoryItem
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(120);
            entity.Property(i => i.NormalizedName).IsRequired().HasMaxLength(120);
            entity.HasIndex(i => i.NormalizedName).IsUnique();
            entity.Property(i => i.Unit).IsRequired().HasMaxLength(30);
            entity.Property(i => i.CurrentQuantity).HasPrecision(18, 4);
            entity.Property(i => i.ReorderThreshold).HasPrecision(18, 4);
            entity.Property(i => i.RowVersion).IsRowVersion();

            entity.HasIndex(i => i.IsActive);
        });

        // MenuItemIngredient
        modelBuilder.Entity<MenuItemIngredient>(entity =>
        {
            entity.HasKey(mi => mi.Id);
            entity.Property(mi => mi.QuantityRequired).HasPrecision(18, 4);

            entity.HasOne(mi => mi.MenuItem)
                .WithMany(m => m.Ingredients)
                .HasForeignKey(mi => mi.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mi => mi.InventoryItem)
                .WithMany(i => i.MenuItemIngredients)
                .HasForeignKey(mi => mi.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(mi => new { mi.MenuItemId, mi.InventoryItemId }).IsUnique();
        });

        // InventoryMovement
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(im => im.Id);
            entity.Property(im => im.QuantityChange).HasPrecision(18, 4);
            entity.Property(im => im.Reason).IsRequired().HasMaxLength(250);

            entity.HasOne(im => im.InventoryItem)
                .WithMany(i => i.Movements)
                .HasForeignKey(im => im.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(im => im.Order)
                .WithMany(o => o.InventoryMovements)
                .HasForeignKey(im => im.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(im => im.CreatedAtUtc);
            entity.HasIndex(im => im.InventoryItemId);
        });
    }
}
