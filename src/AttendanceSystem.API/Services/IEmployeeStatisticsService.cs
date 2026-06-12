using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AttendanceSystem.API.Dtos;

namespace AttendanceSystem.API.Services
{
    public interface IEmployeeStatisticsService
    {
        Task<EmployeeStatisticsDto> GetEmployeeStatisticsAsync(Guid employeeId);
        Task<List<AttendanceHistoryDto>> GetAttendanceHistoryAsync(Guid employeeId, DateOnly from, DateOnly to);
        Task<LeaveStatisticsDto> GetLeaveStatisticsAsync(Guid employeeId);
        Task<GpsStatisticsDto> GetGpsStatisticsAsync(Guid employeeId);
    }
}
