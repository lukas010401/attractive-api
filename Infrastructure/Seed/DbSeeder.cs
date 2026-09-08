using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using AttractiveCatalog.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, IConfiguration configuration, IPasswordService passwordService)
    {
        if (!await dbContext.Categories.AnyAsync())
        {
            dbContext.Categories.AddRange(
                new Category { Name = "Soins visage", Slug = "soins-visage", DisplayOrder = 1 },
                new Category { Name = "Soins corps", Slug = "soins-corps", DisplayOrder = 2 },
                new Category { Name = "Cheveux", Slug = "cheveux", DisplayOrder = 3 },
                new Category { Name = "Parfums", Slug = "parfums", DisplayOrder = 4 },
                new Category { Name = "Maquillage", Slug = "maquillage", DisplayOrder = 5 });
        }

        if (!await dbContext.Brands.AnyAsync())
        {
            dbContext.Brands.AddRange(
                new Brand { Name = "Attractive", Slug = "attractive" },
                new Brand { Name = "Generic Beauty", Slug = "generic-beauty" });
        }

        var adminEmail = (configuration["AdminSeed:Email"] ?? "admin@attractive.mg").Trim().ToLowerInvariant();
        var adminPassword = configuration["AdminSeed:Password"] ?? "Admin123!";

        if (!await dbContext.Users.AnyAsync(x => x.Email == adminEmail))
        {
            dbContext.Users.Add(new User
            {
                Email = adminEmail,
                PasswordHash = passwordService.Hash(adminPassword),
                FullName = "Attractive Administrator",
                PhoneNumber = "000000000",
                Role = UserRole.Admin,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
