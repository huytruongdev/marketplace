using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Listings.SearchService.Models;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;

namespace Listings.SearchService.Services;

public record SearchRequestDto(
    string? Q,
    string? CategoryId,
    string? BrandId,
    string? City,
    string? Condition,
    long? MinPrice,
    long? MaxPrice,
    int Page = 1,
    int PageSize = 20
);

public class ListingSearchService(ElasticsearchClient client, IOptions<ElasticsearchOptions> opt)
{
    private readonly string _indexName = opt.Value.Index;

    public async Task<(long Total, IEnumerable<ListingSearchDocument> Items)> SearchAsync(SearchRequestDto req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var pageSize = req.PageSize is < 1 or > 50 ? 20 : req.PageSize;
        var from = (page - 1) * pageSize;

        var response = await client.SearchAsync<ListingSearchDocument>(s => s
            .Indices(_indexName)
            .From(from)
            .Size(pageSize)
            .Query(q => q.Bool(b => b
                .Must(BuildMustQuery(req.Q))
                .Filter(BuildFilterQuery(req))
            ))
            .Sort(sort => sort.Field(f => f.CreatedAtUtc, so => so.Order(SortOrder.Desc)))
        , ct);

        if (!response.IsValidResponse)
        {
            // Log error here if needed
            return (0, Enumerable.Empty<ListingSearchDocument>());
        }

        return (response.Total, response.Documents);
    }

    public async Task<IEnumerable<string>> SuggestAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var response = await client.SearchAsync<ListingSearchDocument>(s => s
            .Indices(_indexName)
            .Size(8)
            .Source(sf => sf.Filter(i => i.Includes(f => f.Title))) // Chỉ lấy Title
            .Query(q => q.MultiMatch(mm => mm
                .Query(query)
                .Type(TextQueryType.BoolPrefix)
                .Fields(new[] { "title", "title._2gram", "title._3gram" }) // Field đặc biệt vẫn phải dùng string
            ))
        , ct);

        return response.Documents
            .Select(d => d.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(8);
    }


    private static Action<QueryDescriptor<ListingSearchDocument>>[] BuildMustQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [m => m.MatchAll()];
        }

        return
        [
            m => m.MultiMatch(mm => mm
                .Query(query)
                .Type(TextQueryType.BoolPrefix)
                .Fields(new[] { "title", "title._2gram", "title._3gram" }))
        ];
    }

    private static Action<QueryDescriptor<ListingSearchDocument>>[] BuildFilterQuery(SearchRequestDto req)
    {
        var filters = new List<Action<QueryDescriptor<ListingSearchDocument>>>();

        // Dùng Lambda (f.CategoryId) thay vì string "categoryId" để Type-Safe
        if (!string.IsNullOrWhiteSpace(req.CategoryId))
            filters.Add(q => q.Term(t => t.Field(f => f.CategoryId).Value(req.CategoryId)));

        if (!string.IsNullOrWhiteSpace(req.BrandId))
            filters.Add(q => q.Term(t => t.Field(f => f.BrandId).Value(req.BrandId)));

        if (!string.IsNullOrWhiteSpace(req.City))
            filters.Add(q => q.Term(t => t.Field(f => f.City).Value(req.City)));

        if (!string.IsNullOrWhiteSpace(req.Condition))
            filters.Add(q => q.Term(t => t.Field(f => f.Condition).Value(req.Condition)));

        if (req.MinPrice.HasValue || req.MaxPrice.HasValue)
        {
            filters.Add(q => q.Range(r => r.Number(n => n
                .Field(f => f.Price)
                .Gte(req.MinPrice.HasValue ? (double?)req.MinPrice.Value : null)
                .Lte(req.MaxPrice.HasValue ? (double?)req.MaxPrice.Value : null)
            )));
        }

        return [.. filters];
    }
}