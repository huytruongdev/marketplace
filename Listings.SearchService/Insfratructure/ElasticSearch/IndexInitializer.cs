using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Listings.SearchService.Options;

namespace Listings.SearchService.Insfratructure.ElasticSearch;

public sealed class IndexInitializer
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _opt;

    public IndexInitializer(ElasticsearchClient client, ElasticsearchOptions opt)
    {
        _client = client;
        _opt = opt;
    }

    public async Task EnsureCreatedAsync(CancellationToken ct)
    {
        var exists = await _client.Indices.ExistsAsync(_opt.Index, ct);
        if (exists.Exists) return;

        var create = await _client.Indices.CreateAsync(_opt.Index, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0)
            )
            .Mappings(m => m.Properties(ps => ps
                // title: search-as-you-type
                .SearchAsYouType("title", t => t)

                // filters: keyword
                .Keyword("categoryId")
                .Keyword("brandId")
                .Keyword("city")
                .Keyword("condition")

                // sortable/range
                .Number("price", n => n.Type(NumberType.Long))
                .Date("createdAtUtc")
            ))
        , ct);

        if (!create.Acknowledged)
            throw new InvalidOperationException("Failed to create index: " + _opt.Index);
    }
}
