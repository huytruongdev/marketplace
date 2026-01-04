using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Listings.CommandService.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _opt;

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private RabbitMqEventPublisher(RabbitMqOptions opt, IConnection connection, IChannel channel)
    {
        _opt = opt;
        _connection = connection;
        _channel = channel;
    }

    public static async Task<RabbitMqEventPublisher> CreateAsync(RabbitMqOptions opt, CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = opt.Host,
            Port = opt.Port,
            UserName = opt.Username,
            Password = opt.Password
        };

        var connection = await factory.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: opt.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        return new RabbitMqEventPublisher(opt, connection, channel);
    }

    public async Task PublishAsync(string eventType, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var props = new BasicProperties
        {
            Persistent = true,
            Type = eventType
        };

        // routingKey dùng eventType: ListingCreated / ListingUpdated...
        await _channel.BasicPublishAsync(
            exchange: _opt.Exchange,
            routingKey: eventType,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        _channel.Dispose();
        _connection.Dispose();
    }
}
