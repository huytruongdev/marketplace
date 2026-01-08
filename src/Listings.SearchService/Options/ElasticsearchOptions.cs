namespace Listings.SearchService.Options;

public sealed class ElasticsearchOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = "http://localhost:9200";
    public string Index { get; set; } = "listings_v1";
}
