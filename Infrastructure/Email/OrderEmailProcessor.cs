using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Infrastructure.Config;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttractiveCatalog.Api.Infrastructure.Email;

public sealed class OrderEmailProcessor(
    IServiceScopeFactory scopeFactory,
    IOrderEmailSender sender,
    IOptions<OrderEmailOptions> options,
    ILogger<OrderEmailProcessor> logger)
{
    private static readonly int[] RetryDelaySeconds = [60, 300, 900, 3600, 10800, 21600, 43200, 86400];
    private const int MaxAttempts = 9;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return 0;
        var processed = 0;
        for (var index = 0; index < options.Value.BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var notification = await ClaimAsync(cancellationToken);
            if (notification is null) break;

            string? errorCode = null;
            if (notification.AttemptCount > MaxAttempts)
            {
                errorCode = "RetryLimitReached";
            }
            else
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.SendTimeoutSeconds));
                    await sender.SendAsync(notification, timeout.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    errorCode = exception is SmtpCommandException smtpException
                        ? $"SMTP_{(int)smtpException.StatusCode}"
                        : exception.GetType().Name;
                    errorCode = errorCode[..Math.Min(errorCode.Length, 100)];
                }
            }

            if (!await CompleteAsync(notification, errorCode, cancellationToken))
            {
                logger.LogWarning("Order email {NotificationId} lost its processing lease.", notification.Id);
            }
            else if (errorCode is null)
            {
                logger.LogInformation("Order email {NotificationId} for order {OrderId} accepted by SMTP.", notification.Id, notification.OrderId);
            }
            else if (notification.AttemptCount >= MaxAttempts)
            {
                logger.LogError("Order email {NotificationId} for order {OrderId} failed after {Attempts} attempts. Error code: {ErrorCode}.", notification.Id, notification.OrderId, notification.AttemptCount, errorCode);
            }
            else
            {
                logger.LogWarning("Order email {NotificationId} will be retried after attempt {Attempt}. Error code: {ErrorCode}.", notification.Id, notification.AttemptCount, errorCode);
            }
            processed++;
        }
        return processed;
    }

    private async Task<OrderEmailNotification?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var now = DateTime.UtcNow;
        var query = dbContext.Database.IsRelational()
            ? dbContext.OrderEmailNotifications.FromSqlInterpolated($"""
                SELECT * FROM order_email_notifications
                WHERE "SentAt" IS NULL AND "FailedAt" IS NULL AND "NextAttemptAt" <= {now}
                    AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= {now})
                ORDER BY "NextAttemptAt", "CreatedAt", "Id"
                LIMIT 1 FOR UPDATE SKIP LOCKED
                """)
            : dbContext.OrderEmailNotifications
                .Where(notification => notification.SentAt == null && notification.FailedAt == null
                    && notification.NextAttemptAt <= now && (notification.LeaseExpiresAt == null || notification.LeaseExpiresAt <= now))
                .OrderBy(notification => notification.NextAttemptAt).ThenBy(notification => notification.CreatedAt).ThenBy(notification => notification.Id).Take(1);
        var notification = await query.SingleOrDefaultAsync(cancellationToken);
        if (notification is not null)
        {
            notification.LeaseToken = Guid.NewGuid();
            notification.LeaseExpiresAt = now.AddMinutes(2);
            notification.AttemptCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return notification;
    }

    private async Task<bool> CompleteAsync(OrderEmailNotification notification, string? errorCode, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var query = dbContext.Database.IsRelational()
            ? dbContext.OrderEmailNotifications.FromSqlInterpolated($"""
                SELECT * FROM order_email_notifications
                WHERE "Id" = {notification.Id} AND "LeaseToken" = {notification.LeaseToken}
                FOR UPDATE
                """)
            : dbContext.OrderEmailNotifications.Where(current => current.Id == notification.Id && current.LeaseToken == notification.LeaseToken);
        var current = await query.SingleOrDefaultAsync(cancellationToken);
        if (current is null || current.SentAt is not null || current.FailedAt is not null) return false;

        var now = DateTime.UtcNow;
        current.LeaseToken = null;
        current.LeaseExpiresAt = null;
        current.LastErrorCode = errorCode;
        if (errorCode is null)
        {
            current.SentAt = now;
        }
        else if (current.AttemptCount >= MaxAttempts)
        {
            current.FailedAt = now;
        }
        else
        {
            current.NextAttemptAt = now.AddSeconds(RetryDelaySeconds[current.AttemptCount - 1]);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
