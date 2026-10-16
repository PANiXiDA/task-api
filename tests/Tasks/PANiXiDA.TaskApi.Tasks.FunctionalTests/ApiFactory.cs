using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

namespace PANiXiDA.TaskApi.Tasks.FunctionalTests;

public sealed class ApiFactory(Dictionary<string, string?> settings, SaveChangesInterceptor? interceptor = null, int? rabbitMqPort = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        if (rabbitMqPort is not null)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RabbitMq:Port"] = rabbitMqPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        if (interceptor is not null)
        {
            builder.ConfigureServices(services => services.AddDbContext<TasksDbContext>(options =>
                options.AddInterceptors(interceptor)));
        }
    }
}
