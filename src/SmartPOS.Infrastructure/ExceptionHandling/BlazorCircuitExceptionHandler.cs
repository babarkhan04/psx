using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Web;
using SmartPOS.Application.Common.Interfaces;

namespace SmartPOS.Infrastructure.ExceptionHandling;

public sealed class BlazorCircuitExceptionHandler(
    IErrorLogService errorLogService,
    ILogger<BlazorCircuitExceptionHandler> logger) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogDebug("Blazor circuit opened: {CircuitId}", circuit.Id);
        await Task.CompletedTask;
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogDebug("Blazor circuit closed: {CircuitId}", circuit.Id);
        await Task.CompletedTask;
    }

    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogWarning("Blazor circuit connection down: {CircuitId}", circuit.Id);
        await Task.CompletedTask;
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor circuit connection restored: {CircuitId}", circuit.Id);
        await Task.CompletedTask;
    }
}

public sealed class BlazorErrorBoundaryLogger(
    IErrorLogService errorLogService,
    ILogger<BlazorErrorBoundaryLogger> logger) : IErrorBoundaryLogger
{
    public ValueTask LogErrorAsync(Exception exception)
    {
        logger.LogError(exception, "Blazor component error boundary triggered.");

        return new ValueTask(errorLogService.LogAsync(
            exception,
            source: "BLAZOR",
            errorCode: exception is Domain.Exceptions.SmartPosException spe ? spe.ErrorCode : "UI_ERROR",
            statusCode: exception is Domain.Exceptions.SmartPosException spe2 ? spe2.StatusCode : 500));
    }
}
