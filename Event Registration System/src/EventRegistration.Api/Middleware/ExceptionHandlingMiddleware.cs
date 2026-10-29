using System.Net;
using System.Text.Json;
using EventRegistration.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Api.Middleware;

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
        var statusCode = HttpStatusCode.InternalServerError;
        var title = "An unexpected error occurred.";
        var detail = "An internal server error occurred while processing your request.";

        switch (exception)
        {
            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                title = "Not Found";
                detail = notFoundEx.Message;
                break;

            case ConflictException conflictEx:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = conflictEx.Message;
                break;

            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Bad Request";
                detail = valEx.Message;
                break;

            case DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx):
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = "A record with the specified unique values already exists.";
                _logger.LogWarning(dbEx, "Unique constraint violation handled.");
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        var responseJson = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(responseJson);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            return sqlEx.Number is 2601 or 2627;
        }

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Registrations_EventId_NormalizedEmail", StringComparison.OrdinalIgnoreCase);
    }
}
