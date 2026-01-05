namespace Listings.SearchService.Models;

public sealed class ListingSearchDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;

    public string CategoryId { get; set; } = default!;
    public string BrandId { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Condition { get; set; } = default!;

    public long Price { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
