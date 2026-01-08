namespace Listings.SearchService.Insfratructure.ElasticSearch;

public interface IProjectionHandler
{
    Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct);
}
