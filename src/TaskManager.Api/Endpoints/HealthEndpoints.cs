namespace TaskManager.Api.Endpoints;

internal sealed record HealthStatus(string Status);

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => TypedResults.Ok(new HealthStatus("healthy")))
            .AllowAnonymous()
            .WithTags("Health")
            .Produces<HealthStatus>();

        return app;
    }
}
