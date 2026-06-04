using AttendanceSystem.Domain.Entities;

namespace AttendanceSystem.Application.Interfaces.Repositories;

/// <summary>
/// Persistence for employees.
/// </summary>
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Employee?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Employee?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
    void Update(Employee employee);
}
