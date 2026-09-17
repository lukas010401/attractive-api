using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Infrastructure.Media;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using AttractiveCatalog.Api.Infrastructure.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/metadata")]
public sealed class AdminMetadataController(AppDbContext dbContext, IProductImageStorage imageStorage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var brands = await dbContext.Brands.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BrandResponse(x.Id, x.Name, x.Slug, x.IsActive, x.LogoUrl))
            .ToListAsync(cancellationToken);
        return Ok(new { categories, brands });
    }

    [HttpGet("brands")]
    public async Task<IActionResult> ListBrands([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 8, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Brands.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsActive);
            if (status.Equals("hidden", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch) || x.Slug.ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var brands = await query.OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminBrandListItem(x.Id, x.Name, x.Slug, x.IsActive, x.LogoUrl))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<AdminBrandListItem>(brands, totalCount, page, pageSize));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : SlugHelper.Generate(request.Slug),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(category);
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null) return NotFound();
        category.Name = request.Name.Trim();
        category.Slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : SlugHelper.Generate(request.Slug);
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(category);
    }

    [HttpPost("brands")]
    public async Task<IActionResult> CreateBrand([FromBody] UpsertBrandRequest request, CancellationToken cancellationToken)
    {
        var brand = new Brand
        {
            Name = request.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : SlugHelper.Generate(request.Slug),
            IsActive = request.IsActive
        };
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BrandResponse(brand.Id, brand.Name, brand.Slug, brand.IsActive, brand.LogoUrl));
    }

    [HttpPut("brands/{id:guid}")]
    public async Task<IActionResult> UpdateBrand(Guid id, [FromBody] UpsertBrandRequest request, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (brand is null) return NotFound();
        brand.Name = request.Name.Trim();
        brand.Slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : SlugHelper.Generate(request.Slug);
        brand.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BrandResponse(brand.Id, brand.Name, brand.Slug, brand.IsActive, brand.LogoUrl));
    }

    [HttpPost("brands/{id:guid}/logo")]
    public async Task<IActionResult> UploadBrandLogo(Guid id, List<IFormFile> files, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (brand is null) return NotFound();
        if (files.Count == 0) return BadRequest(new { message = "Aucun fichier reçu." });

        var storedLogo = await imageStorage.SaveBrandLogoAsync(id, files[0], cancellationToken);
        await imageStorage.DeleteAsync(brand.LogoStorageKey, null, cancellationToken);

        brand.LogoUrl = storedLogo.Url;
        brand.LogoStorageKey = storedLogo.StorageKey;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BrandResponse(brand.Id, brand.Name, brand.Slug, brand.IsActive, brand.LogoUrl));
    }
}

public sealed record BrandResponse(Guid Id, string Name, string Slug, bool IsActive, string? LogoUrl);
public sealed record AdminBrandListItem(Guid Id, string Name, string Slug, bool IsActive, string? LogoUrl);
public sealed record UpsertCategoryRequest(string Name, string? Slug, int DisplayOrder, bool IsActive);
public sealed record UpsertBrandRequest(string Name, string? Slug, bool IsActive);
