using Elastic.Clients.Elasticsearch;
using Listings.SearchService.Models;
using Listings.SearchService.Options;

namespace Listings.SearchService.Insfratructure.ElasticSearch;

public sealed class IndexInitializer(ElasticsearchClient client, ElasticsearchOptions opt)
{
    public async Task EnsureCreatedAsync(CancellationToken ct)
    {
        var exists = await client.Indices.ExistsAsync(opt.Index, ct);
        if (exists.Exists) return;

        var create = await client.Indices.CreateAsync<ListingSearchDocument>(opt.Index, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0)
            )
            .Mappings(m => m
                .Properties(ps => ps
                    .SearchAsYouType(t => t.Title)

                    // filters: keyword (Chỉ cần lambda, nó sẽ tự lấy tên field)
                    .Keyword(k => k.CategoryId)
                    .Keyword(k => k.BrandId)
                    .Keyword(k => k.City)
                    .Keyword(k => k.Condition)

                    // sortable/range
                    .LongNumber(n => n.Price)
                    .Date(d => d.CreatedAtUtc)
                )
            )
        , ct);

        if (!create.IsValidResponse) // Trong v8 dùng IsValidResponse thay cho Acknowledged để bao quát hơn
            throw new InvalidOperationException($"Failed to create index {opt.Index}: {create.DebugInformation}");
    }
}
