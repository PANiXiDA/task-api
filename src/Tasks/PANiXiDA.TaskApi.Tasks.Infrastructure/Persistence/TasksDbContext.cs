using Microsoft.EntityFrameworkCore;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

public sealed class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();
        task.ToTable("tasks", table =>
        {
            table.HasCheckConstraint("ck_tasks_title", "length(btrim(\"Title\")) > 0");
            table.HasCheckConstraint("ck_tasks_priority", "\"Priority\" IN ('Low', 'Medium', 'High')");
            table.HasCheckConstraint("ck_tasks_completion", "\"IsCompleted\" = (\"CompletedAt\" IS NOT NULL)");
        });
        task.HasKey(x => x.Id);
        task.Property(x => x.Title).IsRequired().HasMaxLength(TaskItem.MaxTitleLength);
        task.Property(x => x.Priority).HasConversion<string>().HasMaxLength(6);
        task.Property(x => x.RowVersion).IsRowVersion();
    }
}
