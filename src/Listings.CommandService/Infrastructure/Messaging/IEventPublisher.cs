namespace Listings.CommandService.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string eventType, ReadOnlyMemory<byte> body, CancellationToken ct);
}
