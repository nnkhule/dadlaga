using AttendanceSystem.Domain.Entities;

namespace AttendanceSystem.Application.Interfaces.Repositories;

/// <summary>
/// Persistence for attendance records.
/// </summary>
public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetTodayRecordAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
    Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default);
    void Update(AttendanceRecord record);
    Task<IReadOnlyList<AttendanceRecord>> GetOpenCheckoutsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetEmployeesWithoutCheckInAsync(DateOnly date, CancellationToken cancellationToken = default);
}
