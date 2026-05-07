using TaskManagement.DataAccess.Context;
using TaskManagement.DataAccess.Repositories;

namespace TaskManagement.DataAccess;

/// <summary>
/// Unit of Work: holds a single DbContext per request and provides access to repositories.
/// </summary>
public class DbSession : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public DbSession(AppDbContext context)
    {
        _context = context;
    }

    public GenericRepository<T> GetRepo<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new GenericRepository<T>(_context);
            _repositories[type] = repo;
        }
        return (GenericRepository<T>)repo;
    }

    public async Task SaveAsync() =>
        await _context.SaveChangesAsync();

    public void Dispose() =>
        _context.Dispose();
}
