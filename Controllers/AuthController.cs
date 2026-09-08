using AttractiveCatalog.Api.Infrastructure.Config;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using AttractiveCatalog.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext dbContext,
    IPasswordService passwordService,
    IJwtTokenService tokenService,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive || !passwordService.Verify(request.Password, user.PasswordHash)) return Unauthorized("Invalid credentials.");

        return await IssueTokensAsync(user, cancellationToken);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

        var user = await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.Email, x.FullName, x.PhoneNumber, Role = x.Role.ToString() })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);
        if (existing is null || !existing.IsActive || existing.User is null || !existing.User.IsActive) return Unauthorized("Invalid refresh token.");

        existing.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await IssueTokensAsync(existing.User, cancellationToken);
    }

    private async Task<TokenResponse> IssueTokensAsync(Domain.Entities.User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refresh = tokenService.GenerateRefreshToken(jwtOptions.Value.RefreshTokenDays);
        dbContext.RefreshTokens.Add(new Domain.Entities.RefreshToken { UserId = user.Id, Token = refresh.Token, ExpiresAt = refresh.ExpiresAtUtc });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TokenResponse(accessToken, refresh.Token, DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes));
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
