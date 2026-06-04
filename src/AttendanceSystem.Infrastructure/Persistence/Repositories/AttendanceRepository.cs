using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAttendanceRepository"/>.
/// </summary>
public class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _context;

    public AttendanceRepository(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.AttendanceRecords.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public async Task<AttendanceRecord?> GetTodayRecordAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken = default)
        => await _context.AttendanceRecords
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
        => await _context.AttendanceRecords
            .Where(x => x.EmployeeId == employeeId && x.Date >= from && x.Date <= to)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
        => await _context.AttendanceRecords.AddAsync(record, cancellationToken);

    /// <inheritdoc />
    public void Update(AttendanceRecord record) => _context.AttendanceRecords.Update(record);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecord>> GetOpenCheckoutsAsync(DateOnly date, CancellationToken cancellationToken = default)
        => await _context.AttendanceRecords
            .Where(x => x.Date == date && x.CheckOutTime == null)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetEmployeesWithoutCheckInAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var checkedIn = await _context.AttendanceRecords
            .Where(x => x.Date == date)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);

        return await _context.Employees
            .Where(e => e.IsActive && !checkedIn.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }
}
