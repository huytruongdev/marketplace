using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Listings.SearchService.Models;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Listings.SearchService.Insfratructure.Messaging;

public sealed class RabbitMqSearchConsumer : BackgroundService, IAsyncDisposable
{
    private readonly RabbitMqOptions _mq;
    private readonly ElasticsearchOptions _esOpt;
    private readonly ElasticsearchClient _es;
    private readonly ILogger<RabbitMqSearchConsumer> _logger;

    private IConnection? _conn;
    private IChannel? _ch;

    public RabbitMqSearchConsumer(
        IOptions<RabbitMqOptions> mq,
        IOptions<ElasticsearchOptions> esOpt,
        ElasticsearchClient es,
        ILogger<RabbitMqSearchConsumer> logger)
    {
        _mq = mq.Value;
        _esOpt = esOpt.Value;
        _es = es;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _mq.Host,
            Port = _mq.Port,
            UserName = _mq.Username,
            Password = _mq.Password,
            AutomaticRecoveryEnabled = true
        };

        _conn = await factory.CreateConnectionAsync(ct);
        _ch = await _conn.CreateChannelAsync(cancellationToken: ct);

        await _ch.ExchangeDeclareAsync(_mq.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: ct);
        await _ch.QueueDeclareAsync(_mq.Queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);

        await _ch.QueueBindAsync(_mq.Queue, _mq.Exchange, "ListingCreated", cancellationToken: ct);
        await _ch.QueueBindAsync(_mq.Queue, _mq.Exchange, "ListingUpdated", cancellationToken: ct);
        await _ch.QueueBindAsync(_mq.Queue, _mq.Exchange, "ListingDeleted", cancellationToken: ct);

        await _ch.BasicQosAsync(0, 10, false, ct); // nhận tối đa 10 message/worker “in-flight”
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_ch is null) throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_ch);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var rk = ea.RoutingKey;
                var bodyBytes = ea.Body.ToArray();

                if (rk is "ListingCreated" or "ListingUpdated")
                {
                    var doc = JsonSerializer.Deserialize<ListingSearchDocument>(bodyBytes)
                              ?? throw new InvalidOperationException("Invalid payload");

                    // Upsert theo Id (idempotent)
                    var idx = await _es.IndexAsync(doc, i => i
                       .Index(_esOpt.Index)
                       .Id(doc.Id),
                   CancellationToken.None);

                    if (!idx.IsValidResponse)
                        throw new InvalidOperationException($"Elasticsearch index failed: {idx.ElasticsearchServerError?.Error?.Reason}");
                }
                else if (rk is "ListingDeleted")
                {
                    using var parsed = JsonDocument.Parse(bodyBytes);

                    if (!parsed.RootElement.TryGetProperty("id", out var idEl))
                        throw new InvalidOperationException("ListingDeleted payload missing 'id'");

                    var id = idEl.GetGuid();

                    var del = await _es.DeleteAsync<ListingSearchDocument>(id.ToString(), d => d
                        .Index(_esOpt.Index),
                        CancellationToken.None);

                    // Delete 404 cũng coi như OK (idempotent)
                    if (!del.IsValidResponse && del.ElasticsearchServerError?.Status != 404)
                        throw new InvalidOperationException($"Elasticsearch delete failed: {del.ElasticsearchServerError?.Error?.Reason}");
                }

                await _ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handle event failed (rk={RoutingKey})", ea.RoutingKey);

                // nếu service đang stop thì đừng requeue gây loop
                var requeue = !stoppingToken.IsCancellationRequested;

                await _ch.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: requeue,
                    cancellationToken: stoppingToken);
            }
        };

        var tag = await _ch.BasicConsumeAsync(_mq.Queue, autoAck: false, consumer, stoppingToken);
        _logger.LogInformation("RabbitMQ consuming started. tag={Tag}", tag);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_ch is not null) await _ch.DisposeAsync(); } catch { }
        try { _conn?.Dispose(); } catch { }
    }
}
