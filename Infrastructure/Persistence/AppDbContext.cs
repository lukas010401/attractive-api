using AttractiveCatalog.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<SiteVisit> SiteVisits => Set<SiteVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.PasswordHash).HasMaxLength(300);
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.PhoneNumber).HasMaxLength(30);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.Property(x => x.Token).HasMaxLength(300);
            entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Slug).HasMaxLength(120);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("brands");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(140);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CategoryId);
            entity.HasIndex(x => x.BrandId);
            entity.HasIndex(x => x.Price);
            entity.HasIndex(x => x.PublishedAt);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Slug).HasMaxLength(220);
            entity.Property(x => x.ShortDescription).HasMaxLength(400);
            entity.Property(x => x.Sku).HasMaxLength(80);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("product_images");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ProductId);
            entity.Property(x => x.Url).HasMaxLength(500);
            entity.Property(x => x.StorageKey).HasMaxLength(300);
            entity.Property(x => x.CardUrl).HasMaxLength(500);
            entity.Property(x => x.CardStorageKey).HasMaxLength(300);
            entity.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductSpecification>(entity =>
        {
            entity.ToTable("product_specifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ProductId);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Value).HasMaxLength(1000);
            entity.HasOne(x => x.Product).WithMany(x => x.Specifications).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Reference).HasMaxLength(40);
            entity.Property(x => x.CustomerName).HasMaxLength(150);
            entity.Property(x => x.CustomerPhone).HasMaxLength(30);
            entity.Property(x => x.DeliveryAddress).HasMaxLength(500);
            entity.Property(x => x.CustomerNote).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.ProductId);
            entity.Property(x => x.ProductName).HasMaxLength(180);
            entity.Property(x => x.ProductSku).HasMaxLength(80);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.ToTable("admin_audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => x.EntityType);
            entity.Property(x => x.AdminEmail).HasMaxLength(200);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
        });

        modelBuilder.Entity<SiteVisit>(entity =>
        {
            entity.ToTable("site_visits");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.SessionKey);
            entity.Property(x => x.SessionKey).HasMaxLength(120);
            entity.Property(x => x.Path).HasMaxLength(300);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.CreatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = utcNow;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = utcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}


