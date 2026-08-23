using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class NumberSequenceService(
    AppDbContext db,
    IDateTimeProvider clock) : INumberSequenceService
{
    public async Task<string> GetNextAsync(
        Guid tenantId,
        Guid storeId,
        string sequenceType,
        string prefix,
        DateTime sequenceDate,
        int padding = 4,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required to generate a document number.");
        }

        if (storeId == Guid.Empty)
        {
            throw new InvalidOperationException("StoreId is required to generate a document number.");
        }

        if (string.IsNullOrWhiteSpace(sequenceType))
        {
            throw new InvalidOperationException("SequenceType is required to generate a document number.");
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException("Prefix is required to generate a document number.");
        }

        var normalizedType = sequenceType.Trim().ToUpperInvariant();
        var normalizedDate = sequenceDate.Date;
        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        var normalizedPadding = padding <= 0 ? 4 : padding;

        // This method is expected to run inside the caller's Serializable transaction.
        // UPDLOCK + HOLDLOCK prevents two cashiers from reading the same sequence row.
        var sequence = await db.NumberSequences
            .FromSqlInterpolated($@"
                SELECT *
                FROM [NumberSequences] WITH (UPDLOCK, HOLDLOCK)
                WHERE [TenantId] = {tenantId}
                  AND [StoreId] = {storeId}
                  AND [SequenceType] = {normalizedType}
                  AND [SequenceDate] = {normalizedDate}")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(ct);

        if (sequence is null)
        {
            sequence = new NumberSequence
            {
                TenantId = tenantId,
                StoreId = storeId,
                SequenceType = normalizedType,
                SequenceDate = normalizedDate,
                Prefix = normalizedPrefix,
                CurrentNumber = 0,
                Padding = normalizedPadding
            };

            db.NumberSequences.Add(sequence);
        }

        sequence.Prefix = normalizedPrefix;
        sequence.Padding = normalizedPadding;
        sequence.CurrentNumber += 1;
        sequence.LastGeneratedOn = clock.UtcNow;
        sequence.LastGeneratedNumber = normalizedPrefix + sequence.CurrentNumber.ToString($"D{normalizedPadding}");

        return sequence.LastGeneratedNumber;
    }
}
