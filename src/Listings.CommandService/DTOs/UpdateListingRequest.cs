namespace Listings.CommandService.DTOs;

public sealed class UpdateListingRequest
{
    public string Title { get; set; } = default!;
    public string CategoryId { get; set; } = default!;
    public string BrandId { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Condition { get; set; } = default!;
    public long Price { get; set; }
}
