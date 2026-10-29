using System.Net;
using System.Text.Json;
using JobBoard.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        ProblemDetails problemDetails;

        switch (exception)
        {
            case NotFoundException notFoundEx:
                _logger.LogInformation(notFoundEx, "Resource not found: {Message}", notFoundEx.Message);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.NotFound,
                    Title = "Not Found",
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path
                };
                break;

            case ConflictException conflictEx:
                _logger.LogWarning(conflictEx, "Conflict occurred: {Message}", conflictEx.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Title = "Conflict",
                    Detail = conflictEx.Message,
                    Instance = context.Request.Path
                };
                break;

            case ValidationException valEx:
                _logger.LogWarning(valEx, "Validation failed: {Message}", valEx.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails = new ValidationProblemDetails(valEx.Errors)
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Validation Error",
                    Detail = valEx.Message,
                    Instance = context.Request.Path
                };
                break;

            default:
                _logger.LogError(exception, "An unhandled error occurred while processing request to {Path}", context.Request.Path);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Instance = context.Request.Path
                };
                break;
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
