using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/orders/{orderId:guid}/email-notifications")]
public sealed class AdminOrderEmailsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid orderId, CancellationToken cancellationToken)
    {
        var notifications = await dbContext.OrderEmailNotifications.AsNoTracking()
            .Where(notification => notification.OrderId == orderId)
            .OrderBy(notification => notification.Recipient)
            .Select(notification => new { notification.Id, notification.Recipient, notification.AttemptCount, notification.NextAttemptAt, notification.SentAt, notification.FailedAt, notification.LastErrorCode })
            .ToListAsync(cancellationToken);
        return Ok(notifications);
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry(Guid orderId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var requeued = await dbContext.OrderEmailNotifications
            .Where(notification => notification.OrderId == orderId && notification.SentAt == null && notification.FailedAt != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.FailedAt, (DateTime?)null)
                .SetProperty(notification => notification.AttemptCount, 0)
                .SetProperty(notification => notification.NextAttemptAt, now)
                .SetProperty(notification => notification.LeaseToken, (Guid?)null)
                .SetProperty(notification => notification.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(notification => notification.LastErrorCode, (string?)null)
                .SetProperty(notification => notification.UpdatedAt, now), cancellationToken);
        return Ok(new { requeued });
    }
}
