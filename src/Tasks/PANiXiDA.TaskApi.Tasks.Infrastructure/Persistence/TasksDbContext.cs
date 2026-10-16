using Microsoft.EntityFrameworkCore;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

public sealed class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();
        task.ToTable("tasks");
        task.HasKey(x => x.Id);
        task.Property(x => x.Title).IsRequired().HasMaxLength(TaskItem.MaxTitleLength);
        task.Property(x => x.Priority).HasConversion<string>().HasMaxLength(6);
        task.Property(x => x.RowVersion).IsRowVersion();
    }
}
