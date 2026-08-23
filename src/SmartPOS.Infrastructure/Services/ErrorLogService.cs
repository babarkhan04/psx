using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Exceptions;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;

namespace SmartPOS.Infrastructure.Services;

public sealed class ErrorLogService(
    AppDbContext dbContext,
    ICorrelationContext correlationContext,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IStoreContext storeContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ErrorLogService> logger) : IErrorLogService
{
    public async Task LogAsync(
        Exception exception,
        string source,
        string? errorCode = null,
        int? statusCode = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var smartPosException = exception as SmartPosException;

        var entry = new ErrorLog
        {
            CorrelationId = correlationContext.CorrelationId,
            TraceId = httpContext?.TraceIdentifier ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
            UserId = currentUser.UserId,
            TenantId = tenantContext.TenantId,
            StoreId = storeContext.StoreId,
            Source = source,
            ErrorCode = errorCode ?? smartPosException?.ErrorCode ?? "UNHANDLED",
            Message = exception.Message,
            Detail = exception.GetType().FullName,
            StackTrace = exception.StackTrace,
            RequestPath = httpContext?.Request.Path.Value,
            HttpMethod = httpContext?.Request.Method,
            StatusCode = statusCode ?? smartPosException?.StatusCode ?? 500,
            Severity = statusCode is >= 500 or null ? "Error" : "Warning",
            OccurredOn = DateTime.UtcNow
        };

        logger.LogError(
            exception,
            "SmartPOS error {ErrorCode} [{CorrelationId}] from {Source}: {Message}",
            entry.ErrorCode,
            entry.CorrelationId,
            source,
            entry.Message);

        dbContext.ErrorLogs.Add(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception persistException)
        {
            logger.LogCritical(
                persistException,
                "Failed to persist error log for correlation {CorrelationId}",
                entry.CorrelationId);
        }
    }
}
