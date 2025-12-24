using System.Text.Json;

namespace Listings.CommandService.Infrastructure;

public static class OutboxExtensions
{
    public static void AddOutbox(this AppDbContext db, string type, object payload)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }
}
