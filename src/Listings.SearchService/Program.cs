using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Listings.SearchService.Insfratructure.ElasticSearch;
using Listings.SearchService.Options;
using Listings.SearchService.Services;
using Microsoft.Extensions.Options;
using Listings.SearchService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection("Elasticsearch"));

builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

    var settings = new ElasticsearchClientSettings(new Uri(opt.Url))
        .Authentication(new BasicAuthentication(opt.Username, opt.Password))
        .DefaultIndex(opt.Index);

    return new ElasticsearchClient(settings);
});

builder.Services.AddSingleton<IndexInitializer>();
builder.Services.AddSingleton<IProjectionHandler, ListingProjectionHandler>();
builder.Services.AddHostedService<RabbitMqSearchConsumer>();

builder.Services.AddSingleton<ListingSearchService>();

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
    [AsParameters] SearchRequestDto request,
    ListingSearchService service,
    CancellationToken ct) =>
{
    var (total, items) = await service.SearchAsync(request, ct);
    return Results.Ok(new { total, items });
});

app.MapGet("/api/suggest", async (
    string q,
    ListingSearchService service,
    CancellationToken ct) =>
{
    var suggestions = await service.SuggestAsync(q, ct);
    return Results.Ok(suggestions);
});

await app.RunAsync();