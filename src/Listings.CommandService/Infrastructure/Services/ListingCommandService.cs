using Listings.CommandService.Domain;
using Listings.CommandService.DTOs;
using Listings.CommandService.Events;
using Microsoft.EntityFrameworkCore;

namespace Listings.CommandService.Infrastructure.Services;

public sealed class ListingCommandService(AppDbContext db) : IListingCommandService
{
    private readonly AppDbContext _db = db;

    public async Task<Guid> CreateAsync(CreateListingRequest req, CancellationToken ct)
    {
        Validate(req.Title, req.CategoryId, req.BrandId, req.City, req.Condition, req.Price);

        var now = DateTime.UtcNow;

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            OwnerUserId = req.OwnerUserId == Guid.Empty ? Guid.NewGuid() : req.OwnerUserId,
            Title = req.Title.Trim(),
            CategoryId = req.CategoryId.Trim(),
            BrandId = req.BrandId.Trim(),
            City = req.City.Trim(),
            Condition = req.Condition.Trim(),
            Price = req.Price,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsDeleted = false
        };

        _db.Listings.Add(listing);

        _db.AddOutbox("ListingCreated", new ListingCreated(
            listing.Id, listing.OwnerUserId, listing.Title,
            listing.CategoryId, listing.BrandId, listing.City,
            listing.Condition, listing.Price, listing.CreatedAtUtc
        ));

        await _db.SaveChangesAsync(ct);
        return listing.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateListingRequest req, CancellationToken ct)
    {
        Validate(req.Title, req.CategoryId, req.BrandId, req.City, req.Condition, req.Price);

        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null) return false;

        var now = DateTime.UtcNow;

        listing.Title = req.Title.Trim();
        listing.CategoryId = req.CategoryId.Trim();
        listing.BrandId = req.BrandId.Trim();
        listing.City = req.City.Trim();
        listing.Condition = req.Condition.Trim();
        listing.Price = req.Price;
        listing.UpdatedAtUtc = now;

        _db.AddOutbox("ListingUpdated", new ListingUpdated(
            listing.Id, listing.Title, listing.CategoryId,
            listing.BrandId, listing.City, listing.Condition,
            listing.Price, listing.UpdatedAtUtc
        ));

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        // IgnoreQueryFilters để xóa cả item đã soft delete (idempotent)
        var listing = await _db.Listings.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null) return false;
        if (listing.IsDeleted) return true;

        listing.IsDeleted = true;
        listing.UpdatedAtUtc = DateTime.UtcNow;

        _db.AddOutbox("ListingDeleted", new ListingDeleted(listing.Id, DateTime.UtcNow));
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ListingResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (listing is null) return null;

        return new ListingResponse
        {
            Id = listing.Id,
            OwnerUserId = listing.OwnerUserId,
            Title = listing.Title,
            CategoryId = listing.CategoryId,
            BrandId = listing.BrandId,
            City = listing.City,
            Condition = listing.Condition,
            Price = listing.Price,
            CreatedAtUtc = listing.CreatedAtUtc,
            UpdatedAtUtc = listing.UpdatedAtUtc
        };
    }

    private static void Validate(string title, string categoryId, string brandId, string city, string condition, long price)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required");
        if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("categoryId is required");
        if (string.IsNullOrWhiteSpace(brandId)) throw new ArgumentException("brandId is required");
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("city is required");
        if (string.IsNullOrWhiteSpace(condition)) throw new ArgumentException("condition is required");
        if (price <= 0) throw new ArgumentException("price must be > 0");
    }
}
