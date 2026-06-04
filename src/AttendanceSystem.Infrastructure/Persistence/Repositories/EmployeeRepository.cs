using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IEmployeeRepository"/>.
/// </summary>
public class EmployeeRepository : IEmployeeRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeRepository(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Employees
            .Include(e => e.WorkSchedule)
            .Include(e => e.OfficeLocation)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await _context.Employees
            .Include(e => e.WorkSchedule)
            .Include(e => e.OfficeLocation)
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<Employee?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == code, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Employee>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Employees.Where(e => e.IsActive).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
        => await _context.Employees.AddAsync(employee, cancellationToken);

    /// <inheritdoc />
    public void Update(Employee employee) => _context.Employees.Update(employee);
}
