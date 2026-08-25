using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Api.Common;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public ApiExceptionHandler(IProblemDetailsService problemDetails) => _problemDetails = problemDetails;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (Map(exception) is not { } problem)
        {
            return false;
        }

        context.Response.StatusCode = problem.Status!.Value;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails? Map(Exception exception) => exception switch
    {
        ValidationException validation => ValidationProblem(validation),

        // Parameter binding failures. The framework throws these instead of writing a response
        // whenever RouteHandlerOptions.ThrowOnBadRequest is set, which is the default in
        // Development; unmapped, they reached the fallback handler and became a 500.
        BadHttpRequestException badRequest => Problem(badRequest.StatusCode, "Bad request", badRequest.Message),
        InvalidCredentialsException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
        NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception.Message),
        ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
        DomainException => Problem(StatusCodes.Status400BadRequest, "Bad request", exception.Message),
        _ => null,
    };

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        var problem = Problem(
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            "The request did not pass validation.");

        problem.Extensions["errors"] = exception.Errors
            .GroupBy(failure => ToCamelCase(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

        return problem;
    }

    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
