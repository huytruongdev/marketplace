namespace Listings.CommandService.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "mvp";
    public string Password { get; set; } = "mvp";
    public string Exchange { get; set; } = "listing.events";
}
