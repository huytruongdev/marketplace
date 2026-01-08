using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Listings.SearchService.Models;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;

namespace Listings.SearchService.Insfratructure.ElasticSearch;

public sealed class ListingProjectionHandler : IProjectionHandler
{
    private readonly ElasticsearchClient _es;
    private readonly ElasticsearchOptions _opt;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ListingProjectionHandler(
        ElasticsearchClient es,
        IOptions<ElasticsearchOptions> opt)
    {
        _es = es;
        _opt = opt.Value;
    }

    public async Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (routingKey is "ListingCreated" or "ListingUpdated")
        {
            await UpsertAsync(body, ct);
            return;
        }

        if (routingKey is "ListingDeleted")
        {
            await DeleteAsync(body, ct);
            return;
        }
    }

    private async Task UpsertAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var doc = JsonSerializer.Deserialize<ListingSearchDocument>(body.Span, JsonOpt)
                  ?? throw new InvalidOperationException("Invalid payload: cannot deserialize ListingSearchDocument");

        if (doc.Id == Guid.Empty)
            throw new InvalidOperationException("Invalid payload: Id is empty");

        var resp = await _es.IndexAsync(doc, i => i
            .Index(_opt.Index)
            .Id(doc.Id.ToString())
        // .Refresh(Refresh.WaitFor) // bật khi test muốn search ra ngay
        , ct);

        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"ES index failed: {resp.DebugInformation}");
    }

    private async Task DeleteAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("Invalid payload: missing 'id'");

        var id = idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Invalid payload: empty 'id'");

        var resp = await _es.DeleteAsync<ListingSearchDocument>(id, d => d
            .Index(_opt.Index),
            ct);

        // idempotent: delete 404 vẫn OK
        if (!resp.IsValidResponse && resp.ApiCallDetails?.HttpStatusCode != 404)
            throw new InvalidOperationException($"ES delete failed: {resp.DebugInformation}");
    }
}
