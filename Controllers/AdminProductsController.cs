using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Media;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using AttractiveCatalog.Api.Infrastructure.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/products")]
public sealed class AdminProductsController(AppDbContext dbContext, IProductImageStorage imageStorage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 8, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(normalizedSearch)
                || (x.Category != null && x.Category.Name.ToLower().Contains(normalizedSearch))
                || (x.Brand != null && x.Brand.Name.ToLower().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminProductListItem(
                x.Id,
                x.Name,
                x.Slug,
                x.Price,
                x.Currency,
                x.StockQuantity,
                x.Status.ToString(),
                x.Category == null ? string.Empty : x.Category.Name,
                x.Brand == null ? null : x.Brand.Name,
                x.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                x.IsFeatured))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<AdminProductListItem>(products, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var product = await BuildProductDetailAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProductRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateProductRequestAsync(request, cancellationToken);
        if (validation is not null) return validation;

        var product = new Product();
        Apply(product, request);
        product.Slug = await BuildUniqueSlugAsync(product.Slug, null, cancellationToken);
        foreach (var spec in request.Specifications.OrderBy(x => x.DisplayOrder))
        {
            product.Specifications.Add(new ProductSpecification { Name = spec.Name.Trim(), Value = spec.Value.Trim(), DisplayOrder = spec.DisplayOrder });
        }

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = await BuildProductDetailAsync(product.Id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertProductRequest request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return NotFound();

        var validation = await ValidateProductRequestAsync(request, cancellationToken);
        if (validation is not null) return validation;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Apply(product, request);
        product.Slug = await BuildUniqueSlugAsync(product.Slug, product.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.ProductSpecifications
            .Where(x => x.ProductId == id)
            .ExecuteDeleteAsync(cancellationToken);

        var specifications = request.Specifications
            .OrderBy(x => x.DisplayOrder)
            .Select(spec => new ProductSpecification { ProductId = id, Name = spec.Name.Trim(), Value = spec.Value.Trim(), DisplayOrder = spec.DisplayOrder })
            .ToList();

        dbContext.ProductSpecifications.AddRange(specifications);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await BuildProductDetailAsync(product.Id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadImages(Guid id, List<IFormFile> files, CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products.AnyAsync(x => x.Id == id, cancellationToken);
        if (!productExists) return NotFound();
        if (files.Count == 0) return BadRequest("At least one image is required.");

        var currentImages = await dbContext.ProductImages
            .AsNoTracking()
            .Where(x => x.ProductId == id)
            .Select(x => new { x.DisplayOrder, x.IsPrimary })
            .ToListAsync(cancellationToken);

        var storedImages = await imageStorage.SaveAsync(id, files, cancellationToken);
        var nextOrder = currentImages.Count == 0 ? 0 : currentImages.Max(x => x.DisplayOrder) + 1;
        var shouldSetPrimary = !currentImages.Any(x => x.IsPrimary);

        var productImages = storedImages.Select((image, index) => new ProductImage
        {
            ProductId = id,
            Url = image.Url,
            StorageKey = image.StorageKey,
            DisplayOrder = nextOrder + index,
            IsPrimary = shouldSetPrimary && index == 0
        }).ToList();

        dbContext.ProductImages.AddRange(productImages);
        await dbContext.SaveChangesAsync(cancellationToken);

        var images = await dbContext.ProductImages
            .AsNoTracking()
            .Where(x => x.ProductId == id)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new AdminProductImageDetail(x.Id, x.Url, x.IsPrimary, x.DisplayOrder))
            .ToListAsync(cancellationToken);

        return Ok(images);
    }

    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await dbContext.ProductImages.SingleOrDefaultAsync(x => x.Id == imageId && x.ProductId == productId, cancellationToken);
        if (image is null) return NotFound();

        dbContext.ProductImages.Remove(image);
        await imageStorage.DeleteAsync(image.StorageKey, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return NotFound();

        product.Status = product.StockQuantity > 0 ? ProductStatus.Published : ProductStatus.OutOfStock;
        product.PublishedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = await BuildProductDetailAsync(product.Id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    private async Task<AdminProductDetail?> BuildProductDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                x.BrandId,
                x.Name,
                x.Slug,
                x.ShortDescription,
                x.Description,
                x.Price,
                x.CompareAtPrice,
                x.Currency,
                x.StockQuantity,
                x.Sku,
                Status = x.Status.ToString(),
                x.IsFeatured,
                CategoryName = x.Category == null ? string.Empty : x.Category.Name,
                CategorySlug = x.Category == null ? string.Empty : x.Category.Slug,
                BrandName = x.Brand == null ? null : x.Brand.Name,
                BrandSlug = x.Brand == null ? null : x.Brand.Slug
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null) return null;

        var images = await dbContext.ProductImages.AsNoTracking()
            .Where(x => x.ProductId == id)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new AdminProductImageDetail(x.Id, x.Url, x.IsPrimary, x.DisplayOrder))
            .ToListAsync(cancellationToken);

        var specifications = await dbContext.ProductSpecifications.AsNoTracking()
            .Where(x => x.ProductId == id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new AdminProductSpecificationDetail(x.Id, x.Name, x.Value, x.DisplayOrder))
            .ToListAsync(cancellationToken);

        return new AdminProductDetail(
            product.Id,
            product.CategoryId,
            product.BrandId,
            product.Name,
            product.Slug,
            product.ShortDescription,
            product.Description,
            product.Price,
            product.CompareAtPrice,
            product.Currency,
            product.StockQuantity,
            product.Sku,
            product.Status,
            product.IsFeatured,
            product.CategoryName,
            product.CategorySlug,
            product.BrandName,
            product.BrandSlug,
            images.Select(x => x.Url).FirstOrDefault(),
            images,
            specifications);
    }
    private async Task<IActionResult?> ValidateProductRequestAsync(UpsertProductRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Product name is required.");
        if (request.Price < 0) return BadRequest("Price cannot be negative.");
        if (request.StockQuantity < 0) return BadRequest("Stock quantity cannot be negative.");
        if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken)) return BadRequest("Category does not exist.");
        if (request.BrandId.HasValue && !await dbContext.Brands.AnyAsync(x => x.Id == request.BrandId.Value, cancellationToken)) return BadRequest("Brand does not exist.");
        return null;
    }

    private async Task<string> BuildUniqueSlugAsync(string baseSlug, Guid? currentProductId, CancellationToken cancellationToken)
    {
        var normalizedBaseSlug = string.IsNullOrWhiteSpace(baseSlug) ? Guid.NewGuid().ToString("N") : baseSlug.Trim();
        var candidate = normalizedBaseSlug;
        var suffix = 2;

        while (await dbContext.Products.AnyAsync(x => x.Slug == candidate && (!currentProductId.HasValue || x.Id != currentProductId.Value), cancellationToken))
        {
            candidate = $"{normalizedBaseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static void Apply(Product product, UpsertProductRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.Name = request.Name.Trim();
        product.Slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : SlugHelper.Generate(request.Slug);
        product.ShortDescription = request.ShortDescription?.Trim() ?? string.Empty;
        product.Description = request.Description?.Trim() ?? string.Empty;
        product.Price = request.Price;
        product.CompareAtPrice = request.CompareAtPrice;
        product.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "MGA" : request.Currency.Trim().ToUpperInvariant();
        product.StockQuantity = request.StockQuantity;
        product.Sku = request.Sku?.Trim();
        product.Status = Enum.TryParse<ProductStatus>(request.Status, true, out var status) ? status : ProductStatus.Draft;
        product.IsFeatured = request.IsFeatured;
        if (product.Status == ProductStatus.Published) product.PublishedAt ??= DateTime.UtcNow;
    }
}

public sealed record PaginatedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record AdminProductListItem(Guid Id, string Name, string Slug, decimal Price, string Currency, int StockQuantity, string Status, string CategoryName, string? BrandName, string? PrimaryImageUrl, bool IsFeatured);
public sealed record AdminProductDetail(Guid Id, Guid CategoryId, Guid? BrandId, string Name, string Slug, string ShortDescription, string Description, decimal Price, decimal? CompareAtPrice, string Currency, int StockQuantity, string? Sku, string Status, bool IsFeatured, string CategoryName, string CategorySlug, string? BrandName, string? BrandSlug, string? PrimaryImageUrl, IReadOnlyList<AdminProductImageDetail> Images, IReadOnlyList<AdminProductSpecificationDetail> Specifications);
public sealed record AdminProductImageDetail(Guid Id, string Url, bool IsPrimary, int DisplayOrder);
public sealed record AdminProductSpecificationDetail(Guid Id, string Name, string Value, int DisplayOrder);
public sealed record UpsertProductRequest(Guid CategoryId, Guid? BrandId, string Name, string? Slug, string? ShortDescription, string? Description, decimal Price, decimal? CompareAtPrice, string? Currency, int StockQuantity, string? Sku, string? Status, bool IsFeatured, IReadOnlyList<UpsertProductSpecificationRequest> Specifications);
public sealed record UpsertProductSpecificationRequest(string Name, string Value, int DisplayOrder);


