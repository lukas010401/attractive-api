using AttractiveCatalog.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AttractiveCatalog.Api.Infrastructure.Email;

public sealed class OrderEmailWorker(
    OrderEmailProcessor processor,
    IOptions<OrderEmailOptions> options,
    ILogger<OrderEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        if (string.IsNullOrWhiteSpace(options.Value.Password))
        {
            logger.LogError("Order emails are enabled but OrderEmail:Password is missing. Pending notifications will remain in the database; configure the password and restart the API.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Order email processing failed with {ErrorType}. The next polling cycle will retry.", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
