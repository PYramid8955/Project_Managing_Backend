using System.Net;
using System.Text.Json;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using ValidationException = TaskManagement.BusinessLayer.Structure.Exceptions.ValidationException;

namespace TaskManagement.Api.Middleware;

public class ExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var isServerError = ex is not (NotFoundException or ForbiddenException or
                UnauthorizedException or ConflictException or ValidationException or ArgumentException);

            if (isServerError)
                _logger.LogError(ex, "Unhandled server error on {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning("Client error: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ForbiddenException => (HttpStatusCode.Forbidden, exception.Message),
            UnauthorizedException => (HttpStatusCode.Unauthorized, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            ValidationException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var result = JsonSerializer.Serialize(new
        {
            message,
            statusCode = (int)statusCode
        });

        return context.Response.WriteAsync(result);
    }
}
