using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Tasks;
using TaskManager.Application.Users;

namespace TaskManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(includeInternalTypes: true);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();

        return services;
    }
}
