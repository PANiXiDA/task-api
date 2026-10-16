using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PANiXiDA.TaskApi.Tasks.Domain.Tasks;

namespace PANiXiDA.TaskApi.Tasks.FunctionalTests;

internal sealed class CompletionBarrier : SaveChangesInterceptor
{
    private readonly TaskCompletionSource _bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<TaskItem>()
            .Any(x => x.State == EntityState.Modified && x.Entity.IsCompleted))
        {
            if (Interlocked.Increment(ref _arrived) == 2)
            {
                _bothReady.TrySetResult();
            }

            await _bothReady.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return result;
    }
}
