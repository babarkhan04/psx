using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(string? search = null, bool includeInactive = true, CancellationToken ct = default);
    Task<CustomerDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerLookupDto>> SearchLookupAsync(string? term, int take = 10, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CustomerDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(CustomerDto dto, CancellationToken ct = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> ReceivePaymentAsync(ReceivePaymentDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(Guid customerId, CancellationToken ct = default);
}
