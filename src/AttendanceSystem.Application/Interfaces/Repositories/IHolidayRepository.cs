namespace AttendanceSystem.Application.Interfaces.Repositories;

/// <summary>
/// Persistence for holidays.
/// </summary>
public interface IHolidayRepository
{
    /// <summary>
    /// Checks if a given date is a holiday (recurring yearly or fixed date).
    /// </summary>
    Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default);
}