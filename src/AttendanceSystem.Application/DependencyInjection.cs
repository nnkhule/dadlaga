using System.Reflection;
using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Application;

/// <summary>
/// Application layer service registration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR, validators, and application services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<AttendanceRulesService>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AttendanceRulesOptions>>();
                var holidayRepository = provider.GetRequiredService<IHolidayRepository>();
                return new AttendanceRulesService(options, holidayRepository);
            });
        services.AddSingleton<IGeofenceService, GeofenceService>();
        services.Configure<AttendanceRulesOptions>(_ => { });
        services.Configure<JwtSettings>(_ => { });
        return services;
    }
}