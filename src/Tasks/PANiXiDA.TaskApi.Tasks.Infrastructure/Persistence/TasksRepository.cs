using Microsoft.EntityFrameworkCore;
using PANiXiDA.TaskApi.Tasks.Application.Tasks;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Persistence;

internal sealed class TasksRepository(TasksDbContext dbContext) : ITasksRepository
{
    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Tasks.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Tasks.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Tasks.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken) != 0;
}
