using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Listings.SearchService;
using Listings.SearchService.Insfratructure.ElasticSearch;
using Listings.SearchService.Models;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<ElasticsearchOptions>(
    builder.Configuration.GetSection("Elasticsearch"));

builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

    var settings = new ElasticsearchClientSettings(new Uri(opt.Url))
        .Authentication(new BasicAuthentication("elastic", "your_strong_password"));

    return new ElasticsearchClient(settings);
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value);
builder.Services.AddSingleton<IndexInitializer>();

// RabbitMQ consumer (projection / indexer)
builder.Services.AddHostedService<RabbitMqSearchConsumer>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var init = scope.ServiceProvider.GetRequiredService<IndexInitializer>();
    var lifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
    await init.EnsureCreatedAsync(lifetime.ApplicationStopping);
}

app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/api/search", async (
    string? q,
    string? categoryId,
    string? brandId,
    string? city,
    string? condition,
    long? minPrice,
    long? maxPrice,
    int page,
    int pageSize,
    ElasticsearchClient es,
    ElasticsearchOptions opt,
    CancellationToken ct) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;
    var from = (page - 1) * pageSize;

    var filters = new List<Action<QueryDescriptor<ListingSearchDocument>>>();

    if (!string.IsNullOrWhiteSpace(categoryId))
        filters.Add(f => f.Term(t => t.Field("categoryId").Value(categoryId)));

    if (!string.IsNullOrWhiteSpace(brandId))
        filters.Add(f => f.Term(t => t.Field("brandId").Value(brandId)));

    if (!string.IsNullOrWhiteSpace(city))
        filters.Add(f => f.Term(t => t.Field("city").Value(city)));

    if (!string.IsNullOrWhiteSpace(condition))
        filters.Add(f => f.Term(t => t.Field("condition").Value(condition)));

    if (minPrice.HasValue || maxPrice.HasValue)
    {
        filters.Add(f => f.Range(r => r.Number(nr => nr
            .Field("price")
            .Gte(minPrice.HasValue ? (double?)minPrice.Value : null)
            .Lte(maxPrice.HasValue ? (double?)maxPrice.Value : null)
        )));
    }

    var must = new List<Action<QueryDescriptor<ListingSearchDocument>>>();

    if (string.IsNullOrWhiteSpace(q))
    {
        must.Add(m => m.MatchAll());
    }
    else
    {
        must.Add(m => m.MultiMatch(mm => mm
            .Query(q)
            .Type(TextQueryType.BoolPrefix)
            .Fields(new[] { "title", "title._2gram", "title._3gram" })
        ));
    }

    var resp = await es.SearchAsync<ListingSearchDocument>(s => s
        .Indices(opt.Index)
        .From(from)
        .Size(pageSize)
        .Query(qry => qry.Bool(b => b
            .Must(must.ToArray())
            .Filter(filters.ToArray())
        ))
        .Sort(sort => sort.Field("createdAtUtc", so => so.Order(SortOrder.Desc)))
    , ct);

    return Results.Ok(new
    {
        total = resp.Total,
        items = resp.Hits.Select(h => h.Source)
    });
});

// ======================================================
// SUGGEST API
// ======================================================
app.MapGet("/api/suggest", async (
    string q,
    ElasticsearchClient es,
    ElasticsearchOptions opt,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(Array.Empty<string>());

    var resp = await es.SearchAsync<ListingSearchDocument>(s => s
        .Indices(opt.Index)
        .Size(8)
        .Source(sf => sf.Filter(i => i.Includes(new[] { "id", "title" })))
        .Query(qry => qry.MultiMatch(mm => mm
            .Query(q)
            .Type(TextQueryType.BoolPrefix)
            .Fields(new[] { "title", "title._2gram", "title._3gram" })
        ))
    , ct);

    var suggestions = resp.Hits
        .Select(h => h.Source?.Title)
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Distinct()
        .Take(8)
        .ToArray();

    return Results.Ok(suggestions);
});

await app.RunAsync();
