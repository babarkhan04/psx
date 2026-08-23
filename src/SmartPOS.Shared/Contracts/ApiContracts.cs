namespace SmartPOS.Shared.Contracts;

public sealed class ErrorResponse
{
    public required string TraceReference { get; init; }
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
}
