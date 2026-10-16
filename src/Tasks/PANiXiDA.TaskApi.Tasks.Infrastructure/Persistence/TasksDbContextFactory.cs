using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

public sealed class TasksDbContextFactory : IDesignTimeDbContextFactory<TasksDbContext>
{
    public TasksDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Tasks")
            ?? "Host=localhost;Database=tasks";
        return new TasksDbContext(new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
