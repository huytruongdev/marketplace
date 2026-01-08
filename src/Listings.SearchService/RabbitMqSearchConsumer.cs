using Listings.SearchService.Insfratructure.ElasticSearch;
using Listings.SearchService.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Listings.SearchService;

public sealed class RabbitMqSearchConsumer : BackgroundService, IAsyncDisposable
{
    private readonly RabbitMqOptions _opt;
    private readonly IProjectionHandler _handler;
    private readonly ILogger<RabbitMqSearchConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqSearchConsumer(
        IOptions<RabbitMqOptions> opt,
        IProjectionHandler handler,
        ILogger<RabbitMqSearchConsumer> logger)
    {
        _opt = opt.Value;
        _handler = handler;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opt.Host,
            Port = _opt.Port,
            UserName = _opt.Username,
            Password = _opt.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: _opt.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: _opt.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // bind keys
        var keys = new[] { "ListingCreated", "ListingUpdated", "ListingDeleted" };
        foreach (var k in keys)
            await _channel.QueueBindAsync(_opt.Queue, _opt.Exchange, k, cancellationToken: cancellationToken);

        // 1 message / consumer
        await _channel.BasicQosAsync(0, 1, false, cancellationToken);

        _logger.LogInformation("SearchService ready. Exchange={Exchange} Queue={Queue}", _opt.Exchange, _opt.Queue);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await _handler.HandleAsync(ea.RoutingKey, ea.Body, stoppingToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                _logger.LogInformation("ACK {RoutingKey} tag={Tag}", ea.RoutingKey, ea.DeliveryTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NACK {RoutingKey} tag={Tag} (requeue=true)", ea.RoutingKey, ea.DeliveryTag);

                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        var tag = await _channel.BasicConsumeAsync(
            queue: _opt.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Consuming started. Tag={Tag}", tag);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { }
        try { _connection?.Dispose(); } catch { }
    }
}
