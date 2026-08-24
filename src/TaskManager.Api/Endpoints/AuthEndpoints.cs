using System.Security.Claims;
using TaskManager.Api.Common;
using TaskManager.Application.Users;

namespace TaskManager.Api.Endpoints;

internal sealed record ApiInfo(string Name, string Version);

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, CancellationToken cancellationToken) =>
                TypedResults.Created("/api/auth/me", await auth.RegisterAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithSummary("Creates an account and returns an access token.")
            .Produces<AuthResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken cancellationToken) =>
                TypedResults.Ok(await auth.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithSummary("Exchanges credentials for an access token.")
            .Produces<AuthResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (ClaimsPrincipal principal, IAuthService auth, CancellationToken cancellationToken) =>
                TypedResults.Ok(await auth.GetProfileAsync(principal.GetUserId(), cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Returns the profile of the signed-in account.")
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/public", () => TypedResults.Ok(new ApiInfo("TaskManager API", "1.0")))
            .AllowAnonymous()
            .WithSummary("Anonymous endpoint, available without an access token.")
            .Produces<ApiInfo>();

        return app;
    }
}
