using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PANiXiDA.TaskApi.Tasks.Infrastructure.DependencyInjection;
using PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;
using PANiXiDA.TaskApi.Tasks.Presentation.Features.Tasks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapTasks();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
