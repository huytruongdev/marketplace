namespace Listings.SearchService.Apis;

public sealed class SearchRequest
{
    public string? Q { get; set; }
    public string? CategoryId { get; set; }
    public string? BrandId { get; set; }
    public string? City { get; set; }
    public string? Condition { get; set; }

    public long? MinPrice { get; set; }
    public long? MaxPrice { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Sort { get; set; } = "newest"; // newest | price_asc | price_desc
}
