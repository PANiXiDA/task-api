namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public const string Exchange = "task.events";
    public const string RoutingKey = "task.completed";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public int QueueCapacity { get; set; } = 1024;
}
