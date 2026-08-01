using Common.Mediator;
using Everdue.Server.Application.Behaviors;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Entities;
using Everdue.Server.Application.Imports;
using Everdue.Server.Application.WorkItems;

namespace Everdue.Server.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers every <c>IRequestHandler</c>/<c>INotificationHandler</c> in the assembly plus the
    /// validation behaviour. Endpoints send commands and queries; they never see a handler.
    /// </summary>
    public static IServiceCollection AddEverdueApplication(this IServiceCollection services)
    {
        services.AddMediator(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);
            configuration.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Scoped, like the DbContext it writes through: the eight work-item mutation handlers share it
        // within a request rather than each building their own.
        services.AddScoped<WorkItemMutator>();

        // Import/checklist/custom-field collaborators. Each is a narrow reader or rule shared by several handlers, registered rather
        // than newed up so a handler's constructor lists what it needs.
        services.AddScoped<ChecklistProgressReader>();
        services.AddScoped<ChecklistItemAccess>();
        services.AddScoped<CompletionPreconditions>();
        services.AddScoped<EntityCustomFieldWriter>();
        services.AddScoped<EntityImportHandler>();
        services.AddScoped<WorkItemImportHandler>();

        return services;
    }
}
