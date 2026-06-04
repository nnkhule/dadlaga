using FluentValidation;

namespace AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;

/// <summary>
/// Validates check-in command input.
/// </summary>
public class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
    }
}
