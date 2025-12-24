namespace Listings.CommandService.Domain;

public sealed class Listing
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }

    public string Title { get; set; } = default!;
    public string CategoryId { get; set; } = default!;
    public string BrandId { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Condition { get; set; } = default!; // new / like_new / used_95 ...

    public long Price { get; set; } // VND

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}
