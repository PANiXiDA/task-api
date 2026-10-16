using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Tasks")
            ?? throw new InvalidOperationException("ConnectionStrings:Tasks is required.");
        services.AddDbContext<TasksDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITasksRepository, TasksRepository>();
        services.AddScoped<TasksService>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.HostName) && x.Port is > 0 and <= 65535,
                "RabbitMQ host and port are required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.UserName) && !string.IsNullOrEmpty(x.Password),
                "RabbitMQ credentials are required.")
            .Validate(x => x.QueueCapacity > 0, "RabbitMQ queue capacity must be positive.")
            .ValidateOnStart();
        services.AddSingleton<ITaskEventTransport, RabbitMqTransport>();
        services.AddSingleton<TaskEventPublisher>();
        services.AddSingleton<ITaskEventPublisher>(provider => provider.GetRequiredService<TaskEventPublisher>());
        services.AddHostedService(provider => provider.GetRequiredService<TaskEventPublisher>());
        return services;
    }
}
