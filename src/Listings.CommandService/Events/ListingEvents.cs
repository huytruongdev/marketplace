namespace Listings.CommandService.Events;

public sealed record ListingCreated(
    Guid Id,
    Guid OwnerUserId,
    string Title,
    string CategoryId,
    string BrandId,
    string City,
    string Condition,
    long Price,
    DateTime CreatedAtUtc
);

public sealed record ListingUpdated(
    Guid Id,
    string Title,
    string CategoryId,
    string BrandId,
    string City,
    string Condition,
    long Price,
    DateTime UpdatedAtUtc
);

public sealed record ListingDeleted(
    Guid Id,
    DateTime DeletedAtUtc
);
