using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Exceptions;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var errorLogService = scope.ServiceProvider.GetRequiredService<IErrorLogService>();
        var correlationContext = scope.ServiceProvider.GetRequiredService<ICorrelationContext>();

        var smartPosException = exception as SmartPosException;
        var statusCode = smartPosException?.StatusCode ?? (exception is UnauthorizedAccessException
            ? StatusCodes.Status403Forbidden : StatusCodes.Status500InternalServerError);
        var errorCode = smartPosException?.ErrorCode ?? (exception is UnauthorizedAccessException ? "FORBIDDEN" : "UNHANDLED");

        await errorLogService.LogAsync(
            exception,
            source: "HTTP",
            errorCode: errorCode,
            statusCode: statusCode,
            cancellationToken: cancellationToken);

        var response = new ErrorResponse
        {
            TraceReference = correlationContext.CorrelationId,
            ErrorCode = errorCode,
            Message = GetUserMessage(exception, statusCode),
            Detail = environment.IsDevelopment() ? exception.ToString() : null
        };

        if (httpContext.Request.Path.StartsWithSegments("/api") ||
            httpContext.Request.Headers.Accept.Any(x => x?.Contains("application/json") == true))
        {
            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }

        httpContext.Response.Redirect($"/error?trace={response.TraceReference}&code={response.ErrorCode}");
        return true;
    }

    private static string GetUserMessage(Exception exception, int statusCode)
    {
        if (exception is SmartPosException)
        {
            return exception.Message;
        }

        return statusCode >= 500
            ? "Something went wrong. Please try again or contact support with the trace reference."
            : exception.Message;
    }
}

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
