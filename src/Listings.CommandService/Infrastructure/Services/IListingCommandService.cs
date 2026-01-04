using Listings.CommandService.DTOs;

namespace Listings.CommandService.Infrastructure.Services;

public interface IListingCommandService
{
    Task<Guid> CreateAsync(CreateListingRequest req, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, UpdateListingRequest req, CancellationToken ct);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct);
    Task<ListingResponse?> GetByIdAsync(Guid id, CancellationToken ct);
}
