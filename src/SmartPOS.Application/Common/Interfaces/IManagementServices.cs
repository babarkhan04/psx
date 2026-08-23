using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface ICompanyService
{
    Task<CompanyDto?> GetAsync(CancellationToken ct = default);
    Task<Result> UpdateAsync(CompanyDto dto, CancellationToken ct = default);
}

public interface IStoreService
{
    Task<IReadOnlyList<StoreDto>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default);
    Task<StoreDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(StoreDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(StoreDto dto, CancellationToken ct = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IRegisterService
{
    Task<IReadOnlyList<RegisterDto>> GetAllAsync(Guid? storeId = null, CancellationToken ct = default);
    Task<RegisterDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(RegisterDto dto, CancellationToken ct = default);
    Task<Result> UpdateAsync(RegisterDto dto, CancellationToken ct = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IUserAdminService
{
    Task<IReadOnlyList<AppUserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserEditModel?> GetAsync(string id, CancellationToken ct = default);
    Task<Result<string>> CreateAsync(UserEditModel model, CancellationToken ct = default);
    Task<Result> UpdateAsync(UserEditModel model, CancellationToken ct = default);
    Task<Result> SetActiveAsync(string id, bool isActive, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(string id, string newPassword, CancellationToken ct = default);
}

public interface IRoleAdminService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string>> CreateAsync(string name, CancellationToken ct = default);
    Task<Result> DeleteAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionGroupDto>> GetPermissionMatrixAsync(string roleId, CancellationToken ct = default);
    Task<Result> SetPermissionsAsync(string roleId, IEnumerable<string> permissionKeys, CancellationToken ct = default);
}
