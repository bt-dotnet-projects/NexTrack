using ActivityTracker.Models;

namespace ActivityTracker.Data;

public interface IRepository
{
    Task AddActivityAsync(
        ActivityLog log,
        CancellationToken cancellationToken = default);


    Task<List<ActivityLog>> GetUnsyncedAsync(
        int count,
        CancellationToken cancellationToken = default);


    Task MarkSyncedAsync(
        List<long> ids,
        CancellationToken cancellationToken = default);


    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}