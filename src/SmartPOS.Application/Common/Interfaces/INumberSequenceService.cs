namespace SmartPOS.Application.Common.Interfaces;

public interface INumberSequenceService
{
    Task<string> GetNextAsync(
        Guid tenantId,
        Guid storeId,
        string sequenceType,
        string prefix,
        DateTime sequenceDate,
        int padding = 4,
        CancellationToken ct = default);
}
