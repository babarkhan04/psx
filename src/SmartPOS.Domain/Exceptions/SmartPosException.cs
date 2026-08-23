namespace SmartPOS.Domain.Exceptions;

public class SmartPosException : Exception
{
    public SmartPosException(string message, string errorCode = "BUSINESS_ERROR", int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public SmartPosException(string message, Exception innerException, string errorCode = "BUSINESS_ERROR", int statusCode = 400)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}

public sealed class NotFoundException : SmartPosException
{
    public NotFoundException(string message)
        : base(message, "NOT_FOUND", 404)
    {
    }
}

public sealed class UnauthorizedAccessSmartPosException : SmartPosException
{
    public UnauthorizedAccessSmartPosException(string message)
        : base(message, "UNAUTHORIZED", 403)
    {
    }
}

public sealed class ValidationException : SmartPosException
{
    public ValidationException(string message)
        : base(message, "VALIDATION_ERROR", 422)
    {
    }

    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}
