using AttendanceSystem.Application.Interfaces;

namespace AttendanceSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
