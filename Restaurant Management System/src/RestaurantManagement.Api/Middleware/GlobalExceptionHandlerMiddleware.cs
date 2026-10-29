using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Exceptions;

namespace RestaurantManagement.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
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
        var title = "An error occurred while processing your request.";
        var detail = "An internal server error occurred.";

        switch (exception)
        {
            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                title = "Resource Not Found";
                detail = notFoundEx.Message;
                _logger.LogWarning(notFoundEx, "NotFoundException: {Message}", notFoundEx.Message);
                break;

            case ConflictException conflictEx:
                statusCode = HttpStatusCode.Conflict;
                title = "Conflict";
                detail = conflictEx.Message;
                _logger.LogWarning(conflictEx, "ConflictException: {Message}", conflictEx.Message);
                break;

            case BadRequestException badRequestEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Bad Request";
                detail = badRequestEx.Message;
                _logger.LogWarning(badRequestEx, "BadRequestException: {Message}", badRequestEx.Message);
                break;

            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Invalid Argument";
                detail = argEx.Message;
                _logger.LogWarning(argEx, "ArgumentException: {Message}", argEx.Message);
                break;

            case DbUpdateConcurrencyException concurrencyEx:
                statusCode = HttpStatusCode.Conflict;
                title = "Concurrency Conflict";
                detail = "The resource has been modified by another concurrent operation. Please reload and retry.";
                _logger.LogWarning(concurrencyEx, "Concurrency exception occurred.");
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred while processing request.");
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

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
