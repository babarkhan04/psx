using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class CompanyService(AppDbContext db, ITenantContext tenantContext, IDateTimeProvider clock)
    : ICompanyService
{
    public async Task<CompanyDto?> GetAsync(CancellationToken ct = default)
    {
        if (tenantContext.TenantId is null)
        {
            return null;
        }

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, ct);

        return tenant is null ? null : Map(tenant);
    }

    public async Task<Result> UpdateAsync(CompanyDto dto, CancellationToken ct = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure("No active company context.", "NO_TENANT");
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, ct);
        if (tenant is null)
        {
            return Result.Failure("Company profile not found.", "NOT_FOUND");
        }

        tenant.Name = dto.Name.Trim();
        tenant.LegalName = dto.LegalName?.Trim();
        tenant.Email = dto.Email?.Trim();
        tenant.Phone = dto.Phone?.Trim();
        tenant.TaxNumber = dto.TaxNumber?.Trim();
        tenant.LogoUrl = dto.LogoUrl?.Trim();
        tenant.IsActive = dto.IsActive;
        tenant.ModifiedOn = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static CompanyDto Map(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        LegalName = t.LegalName,
        Email = t.Email,
        Phone = t.Phone,
        TaxNumber = t.TaxNumber,
        LogoUrl = t.LogoUrl,
        IsActive = t.IsActive
    };
}
