using AttendanceSystem.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IHolidayRepository"/>.
/// </summary>
public class HolidayRepository : IHolidayRepository
{
    private readonly ApplicationDbContext _context;

    public HolidayRepository(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default)
        => await _context.Holidays.AnyAsync(h =>
            (h.IsRecurringYearly && h.Date.Month == date.Month && h.Date.Day == date.Day) ||
            (!h.IsRecurringYearly && h.Date == date), cancellationToken);
}