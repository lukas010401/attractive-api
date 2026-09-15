using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class PublicProductsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? category, [FromQuery] string? brand, [FromQuery] bool? featured, [FromQuery] int page = 1, [FromQuery] int pageSize = 8, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Products.AsNoTracking()
            .Where(x => x.Status == ProductStatus.Published)
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(search) || x.ShortDescription.ToLower().Contains(search) || x.Description.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category != null && x.Category.Slug == category);
        if (!string.IsNullOrWhiteSpace(brand)) query = query.Where(x => x.Brand != null && x.Brand.Slug == brand);
        if (featured.HasValue) query = query.Where(x => x.IsFeatured == featured.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderByDescending(x => x.IsFeatured)
            .ThenByDescending(x => x.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductListItemResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.ShortDescription,
                x.Price,
                x.CompareAtPrice,
                x.Currency,
                x.StockQuantity,
                x.Category!.Name,
                x.Category.Slug,
                x.Brand == null ? null : x.Brand.Name,
                x.Brand == null ? null : x.Brand.Slug,
                x.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.CardUrl ?? i.Url).FirstOrDefault(),
                x.IsFeatured))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<ProductListItemResponse>(products, totalCount, page, pageSize));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .Where(x => x.Slug == slug && x.Status == ProductStatus.Published)
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Specifications)
            .Select(x => new ProductDetailResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.ShortDescription,
                x.Description,
                x.Price,
                x.CompareAtPrice,
                x.Currency,
                x.StockQuantity,
                x.Sku,
                x.Category!.Name,
                x.Category.Slug,
                x.Brand == null ? null : x.Brand.Name,
                x.Brand == null ? null : x.Brand.Slug,
                x.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => new ProductImageResponse(i.Id, i.Url, i.IsPrimary, i.DisplayOrder)).ToList(),
                x.Specifications.OrderBy(s => s.DisplayOrder).Select(s => new ProductSpecificationResponse(s.Id, s.Name, s.Value, s.DisplayOrder)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new { x.Id, x.Name, x.Slug }).ToListAsync(cancellationToken);
        var brands = await dbContext.Brands.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Slug }).ToListAsync(cancellationToken);
        return Ok(new { categories, brands });
    }
}

public sealed record ProductListItemResponse(Guid Id, string Name, string Slug, string ShortDescription, decimal Price, decimal? CompareAtPrice, string Currency, int StockQuantity, string CategoryName, string CategorySlug, string? BrandName, string? BrandSlug, string? PrimaryImageUrl, bool IsFeatured);
public sealed record ProductDetailResponse(Guid Id, string Name, string Slug, string ShortDescription, string Description, decimal Price, decimal? CompareAtPrice, string Currency, int StockQuantity, string? Sku, string CategoryName, string CategorySlug, string? BrandName, string? BrandSlug, IReadOnlyList<ProductImageResponse> Images, IReadOnlyList<ProductSpecificationResponse> Specifications);
public sealed record ProductImageResponse(Guid Id, string Url, bool IsPrimary, int DisplayOrder);
public sealed record ProductSpecificationResponse(Guid Id, string Name, string Value, int DisplayOrder);
