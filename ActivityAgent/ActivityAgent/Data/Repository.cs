using ActivityTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivityTracker.Data;

public class Repository : IRepository
{
    private readonly TrackerDbContext _db;


    public Repository(TrackerDbContext db)
    {
        _db = db;
    }


    public async Task AddActivityAsync(
        ActivityLog log,
        CancellationToken cancellationToken = default)
    {
        log.CreatedAt = DateTime.UtcNow;
        log.IsSynced = false;

        await _db.ActivityLogs
            .AddAsync(log, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }


    public async Task<List<ActivityLog>> GetUnsyncedAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await _db.ActivityLogs
            .AsNoTracking()
            .Where(x => !x.IsSynced)
            .OrderBy(x => x.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }


    public async Task MarkSyncedAsync(
        List<long> ids,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.ActivityLogs
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);


        foreach (var item in records)
        {
            item.IsSynced = true;
        }


        await _db.SaveChangesAsync(cancellationToken);
    }


    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}