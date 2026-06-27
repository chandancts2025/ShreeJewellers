using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ShreeJewellers.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns RFC 7807 ProblemDetails responses.
/// NEVER exposes stack traces, inner exception details, or SQL errors to clients.
/// Logs full details server-side with a correlation ID for support tracing.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        var correlationId = context.TraceIdentifier;

        // Log the full exception server-side (Serilog will capture stack trace)
        _logger.LogError(exception,
            "Unhandled exception. CorrelationId: {CorrelationId}, Path: {Path}, User: {User}",
            correlationId,
            context.Request.Path,
            context.User?.Identity?.Name ?? "anonymous");

        // Map exception types to HTTP status codes
        var (statusCode, title, detail) = exception switch
        {
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "You are not authorised to perform this action."),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                exception.Message),

            InvalidOperationException => (
                HttpStatusCode.BadRequest,
                "Invalid Operation",
                exception.Message),

            ArgumentNullException => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                "A required parameter was missing."),

            ArgumentException => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                exception.Message),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                "Please try again or contact support if the problem persists.")
        };

        var problem = new ProblemDetails
        {
            Status   = (int)statusCode,
            Title    = title,
            Detail   = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["timestamp"] = DateTime.UtcNow;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
