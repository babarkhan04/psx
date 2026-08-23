using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierListItemDto>> GetAllAsync(string? search = null, bool includeInactive = true, CancellationToken ct = default);
    Task<SupplierDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierLookupDto>> GetLookupAsync(string? term = null, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(SupplierDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(SupplierDto dto, CancellationToken ct = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
