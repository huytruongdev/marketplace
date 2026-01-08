using Elastic.Clients.Elasticsearch;
using Listings.SearchService.Models;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;

namespace Listings.SearchService.Insfratructure.ElasticSearch;
public sealed class IndexInitializer(ElasticsearchClient client, IOptions<ElasticsearchOptions> otp)
{
    private readonly ElasticsearchOptions _opt = otp.Value;

    public async Task EnsureCreatedAsync(CancellationToken ct)
    {
        var exists = await client.Indices.ExistsAsync(_opt.Index, ct);
        if (exists.Exists) return;

        var create = await client.Indices.CreateAsync<ListingSearchDocument>(_opt.Index, c => c
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

        if (!create.IsValidResponse)
            throw new InvalidOperationException($"Failed to create index {_opt.Index}: {create.DebugInformation}");
    }
}
