using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Listings.SearchService;

public sealed class RabbitMqSearchConsumer : BackgroundService, IAsyncDisposable
{
    private readonly RabbitMqOptions _opt;
    private readonly ILogger<RabbitMqSearchConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqSearchConsumer(
        IOptions<RabbitMqOptions> opt,
        ILogger<RabbitMqSearchConsumer> logger)
    {
        _opt = opt.Value;
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

        await _channel.QueueBindAsync(_opt.Queue, _opt.Exchange, "ListingCreated", cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(_opt.Queue, _opt.Exchange, "ListingUpdated", cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(_opt.Queue, _opt.Exchange, "ListingDeleted", cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(0, 1, false, cancellationToken);

        _logger.LogInformation("SearchService ready. Queue={Queue}", _opt.Queue);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        // ✅ ĐÚNG CHUẨN v7
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var routingKey = ea.RoutingKey;
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

                _logger.LogInformation("ReceivedAsync: {RoutingKey}", routingKey);
                _logger.LogInformation("Payload: {Payload}", payload);

                // TODO: deserialize + update Elasticsearch (async)

                await _channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing failed");

                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: _opt.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Consuming started. Tag={Tag}", consumerTag);

        // giữ service sống
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { }
        try { _connection?.Dispose(); } catch { }
    }
}
