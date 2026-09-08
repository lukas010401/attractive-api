using AttractiveCatalog.Api.Domain.Entities;

namespace AttractiveCatalog.Api.Infrastructure.Security;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken(int validDays);
}
