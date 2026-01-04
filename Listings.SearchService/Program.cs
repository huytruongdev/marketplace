using Listings.SearchService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddHostedService<RabbitMqSearchConsumer>();

var host = builder.Build();
host.Run();