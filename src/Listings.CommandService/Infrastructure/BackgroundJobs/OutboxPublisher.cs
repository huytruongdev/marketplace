using System.Text;
using Microsoft.EntityFrameworkCore;
using Listings.CommandService.Infrastructure.Messaging;

namespace Listings.CommandService.Infrastructure.BackgroundJobs;

public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(IServiceScopeFactory scopeFactory, IEventPublisher publisher, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var batch = await db.OutboxMessages
                    .Where(x => x.PublishedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var msg in batch)
                {
                    try
                    {
                        var body = Encoding.UTF8.GetBytes(msg.PayloadJson);

                        await _publisher.PublishAsync(msg.Type, body, stoppingToken);

                        msg.PublishedAtUtc = DateTime.UtcNow;
                        msg.PublishAttempts += 1;
                        msg.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        msg.PublishAttempts += 1;
                        msg.LastError = ex.Message;
                        _logger.LogError(ex, "Publish failed for OutboxMessage {Id} ({Type})", msg.Id, msg.Type);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
