using Application.Abstractions.Messaging;
using Application.Notes.Get;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<GetNotesQuery, List<NoteResponse>>, GetNotesQueryHandler>();

        return services;
    }
}
