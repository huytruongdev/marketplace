using Microsoft.EntityFrameworkCore;
using Listings.CommandService.Infrastructure.Messaging;

namespace Listings.CommandService.Infrastructure.BackgroundJobs;

public sealed class OutboxPublisher : BackgroundService
{
    private const int BatchSize = 50;

    // Khi batch < 50: ngủ lâu
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    // Khi batch == 50: không ngủ (nhưng yield nhỏ để tránh hot loop)
    private static readonly TimeSpan BusyYield = TimeSpan.FromMilliseconds(1);

    // Backoff khi lỗi
    private static readonly TimeSpan ErrorBaseDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ErrorMaxDelay = TimeSpan.FromSeconds(10);

    // Jitter để tránh “dập nhịp”
    private const int JitterMs = 150;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OutboxPublisher> _logger;

    private int _consecutiveFailures = 0;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IEventPublisher publisher,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rnd = Random.Shared;

        while (!stoppingToken.IsCancellationRequested)
        {
            int batchCount = 0;
            int successCount = 0;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var batch = await db.OutboxMessages
                    .Where(x => x.PublishedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                batchCount = batch.Count;

                if (batchCount == 0)
                {
                    _consecutiveFailures = 0;
                    await DelayWithJitter(IdleDelay, rnd, stoppingToken);
                    continue;
                }

                foreach (var msg in batch)
                {
                    try
                    {
                        var body = System.Text.Encoding.UTF8.GetBytes(msg.PayloadJson);
                        await _publisher.PublishAsync(msg.Type, body, stoppingToken);

                        msg.PublishedAtUtc = DateTime.UtcNow;
                        msg.PublishAttempts += 1;
                        msg.LastError = null;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        msg.PublishAttempts += 1;
                        msg.LastError = ex.Message;

                        _logger.LogError(ex,
                            "Publish failed OutboxMessage {Id} ({Type})",
                            msg.Id, msg.Type);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);

                if (successCount > 0) _consecutiveFailures = 0;

                if (batchCount < BatchSize)
                {
                    await DelayWithJitter(IdleDelay, rnd, stoppingToken);
                }
                else
                {
                    // drain nhanh nhưng tránh hot loop
                    await Task.Delay(BusyYield, stoppingToken);
                }

                // Nếu batch đầy nhưng successCount = 0 (toàn fail) thì backoff để tránh spam
                if (batchCount == BatchSize && successCount == 0)
                {
                    await BackoffDelay(rnd, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher loop error");
                await BackoffDelay(rnd, stoppingToken);
            }
        }
    }

    private async Task BackoffDelay(Random rnd, CancellationToken ct)
    {
        _consecutiveFailures++;

        // exponential: base * 2^(n-1), clamp
        var exp = Math.Min(_consecutiveFailures - 1, 10);
        var delayMs = (int)(ErrorBaseDelay.TotalMilliseconds * Math.Pow(2, exp));
        delayMs = Math.Min(delayMs, (int)ErrorMaxDelay.TotalMilliseconds);

        await Task.Delay(delayMs + rnd.Next(0, JitterMs + 1), ct);
    }

    private static Task DelayWithJitter(TimeSpan baseDelay, Random rnd, CancellationToken ct)
    {
        var ms = (int)baseDelay.TotalMilliseconds + rnd.Next(0, JitterMs + 1);
        return Task.Delay(ms, ct);
    }
}
