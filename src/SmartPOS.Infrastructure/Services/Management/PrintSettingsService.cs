using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class PrintSettingsService(
    AppDbContext db,
    ITenantContext tenantContext,
    IStoreAccessService storeAccess,
    DbContextGate gate) : IPrintSettingsService
{
    public Task<ReceiptTemplateDto> GetReceiptTemplateAsync(Guid? storeId = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetReceiptTemplateCoreAsync(storeId, ct), ct);

    public Task<IReadOnlyList<ReceiptTemplateDto>> GetReceiptTemplatesAsync(Guid? storeId = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetReceiptTemplatesCoreAsync(storeId, ct), ct);

    public Task<Result<Guid>> SaveReceiptTemplateAsync(UpdateReceiptTemplateDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SaveReceiptTemplateCoreAsync(dto, ct), ct);

    public Task<PrintStationSettingDto> GetPrintStationAsync(Guid storeId, Guid? registerId = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetPrintStationCoreAsync(storeId, registerId, ct), ct);

    public Task<Result<Guid>> SavePrintStationAsync(UpdatePrintStationSettingDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SavePrintStationCoreAsync(dto, ct), ct);

    private async Task<ReceiptTemplateDto> GetReceiptTemplateCoreAsync(Guid? storeId, CancellationToken ct)
    {
        if (storeId is { } sid && !await storeAccess.CanAccessStoreAsync(sid, ct))
        {
            return DefaultTemplate(null);
        }

        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
        {
            return DefaultTemplate(storeId);
        }

        var template = await db.ReceiptTemplates.AsNoTracking()
            .Include(t => t.Store)
            .Where(t => t.TenantId == tenantId && t.IsActive && (t.StoreId == storeId || t.StoreId == null))
            .OrderByDescending(t => t.StoreId == storeId)
            .ThenByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.ModifiedOn ?? t.CreatedOn)
            .FirstOrDefaultAsync(ct);

        return template is null ? DefaultTemplate(storeId) : Map(template);
    }

    private async Task<IReadOnlyList<ReceiptTemplateDto>> GetReceiptTemplatesCoreAsync(Guid? storeId, CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return [DefaultTemplate(storeId)];
        }

        if (storeId is { } sid && !await storeAccess.CanAccessStoreAsync(sid, ct))
        {
            return [];
        }

        var query = db.ReceiptTemplates.AsNoTracking()
            .Include(t => t.Store)
            .Where(t => t.TenantId == tenantId && t.IsActive);

        if (storeId is { } s)
        {
            query = query.Where(t => t.StoreId == null || t.StoreId == s);
        }

        var templates = await query
            .OrderBy(t => t.StoreId == null ? 0 : 1)
            .ThenByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .Select(t => Map(t))
            .ToListAsync(ct);

        return templates.Count == 0 ? [DefaultTemplate(storeId)] : templates;
    }

    private async Task<Result<Guid>> SaveReceiptTemplateCoreAsync(UpdateReceiptTemplateDto dto, CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<Guid>.Failure("Tenant context is not available.");
        }

        if (dto.StoreId is { } storeId && !await storeAccess.CanAccessStoreAsync(storeId, ct))
        {
            return Result<Guid>.Failure("You do not have access to this store.", "FORBIDDEN");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<Guid>.Failure("Template name is required.", "VALIDATION");
        }

        var template = dto.Id is { } id
            ? await db.ReceiptTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct)
            : null;

        if (template is null)
        {
            template = new ReceiptTemplate { TenantId = tenantId };
            db.ReceiptTemplates.Add(template);
        }

        Apply(template, dto);

        if (template.IsDefault)
        {
            var siblings = await db.ReceiptTemplates
                .Where(t => t.TenantId == tenantId && t.Id != template.Id && t.StoreId == template.StoreId && t.IsDefault)
                .ToListAsync(ct);

            foreach (var sibling in siblings)
            {
                sibling.IsDefault = false;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(template.Id);
    }

    private async Task<PrintStationSettingDto> GetPrintStationCoreAsync(Guid storeId, Guid? registerId, CancellationToken ct)
    {
        if (!await storeAccess.CanAccessStoreAsync(storeId, ct))
        {
            return DefaultStation(storeId, registerId);
        }

        if (tenantContext.TenantId is not { } tenantId)
        {
            return DefaultStation(storeId, registerId);
        }

        var setting = await db.PrintStationSettings.AsNoTracking()
            .Include(s => s.Register)
            .Include(s => s.Store)
            .Where(s => s.TenantId == tenantId && s.StoreId == storeId && (s.RegisterId == registerId || s.RegisterId == null))
            .OrderByDescending(s => s.RegisterId == registerId)
            .ThenByDescending(s => s.ModifiedOn ?? s.CreatedOn)
            .FirstOrDefaultAsync(ct);

        return setting is null ? DefaultStation(storeId, registerId) : Map(setting);
    }

    private async Task<Result<Guid>> SavePrintStationCoreAsync(UpdatePrintStationSettingDto dto, CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result<Guid>.Failure("Tenant context is not available.");
        }

        if (!await storeAccess.CanAccessStoreAsync(dto.StoreId, ct))
        {
            return Result<Guid>.Failure("You do not have access to this store.", "FORBIDDEN");
        }

        if (string.IsNullOrWhiteSpace(dto.StationName))
        {
            return Result<Guid>.Failure("Station name is required.", "VALIDATION");
        }

        var setting = dto.Id is { } id
            ? await db.PrintStationSettings.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct)
            : null;

        if (setting is null)
        {
            setting = new PrintStationSetting { TenantId = tenantId, StoreId = dto.StoreId };
            db.PrintStationSettings.Add(setting);
        }

        setting.StoreId = dto.StoreId;
        setting.RegisterId = dto.RegisterId;
        setting.ReceiptTemplateId = dto.ReceiptTemplateId;
        setting.StationName = dto.StationName.Trim();
        setting.DefaultReceiptMode = NormalizeMode(dto.DefaultReceiptMode);
        setting.UseBrowserPrint = dto.UseBrowserPrint;
        setting.AutoPrintReceipt = dto.AutoPrintReceipt;
        setting.ReceiptCopies = Math.Clamp(dto.ReceiptCopies, 1, 5);
        setting.ReceiptPrinterName = dto.ReceiptPrinterName?.Trim();
        setting.LabelPrinterName = dto.LabelPrinterName?.Trim();
        setting.UseLocalPrintAgent = dto.UseLocalPrintAgent;
        setting.LocalPrintAgentUrl = string.IsNullOrWhiteSpace(dto.LocalPrintAgentUrl) ? "http://localhost:3859" : dto.LocalPrintAgentUrl.Trim().TrimEnd('/');
        setting.EnableCashDrawerKick = dto.EnableCashDrawerKick;
        setting.CashDrawerCommandHex = string.IsNullOrWhiteSpace(dto.CashDrawerCommandHex) ? "1B700019FA" : dto.CashDrawerCommandHex.Trim();

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(setting.Id);
    }

    private static void Apply(ReceiptTemplate template, UpdateReceiptTemplateDto dto)
    {
        template.StoreId = dto.StoreId;
        template.Name = dto.Name.Trim();
        template.IsDefault = dto.IsDefault;
        template.IsActive = dto.IsActive;
        template.PaperSize = NormalizeMode(dto.PaperSize);
        template.BusinessName = dto.BusinessName?.Trim();
        template.Subtitle = dto.Subtitle?.Trim();
        template.LogoUrl = dto.LogoUrl?.Trim();
        template.HeaderText = dto.HeaderText?.Trim();
        template.FooterText = dto.FooterText?.Trim();
        template.ReturnPolicyText = dto.ReturnPolicyText?.Trim();
        template.ShowStoreName = dto.ShowStoreName;
        template.ShowCustomer = dto.ShowCustomer;
        template.ShowCashier = dto.ShowCashier;
        template.ShowPaymentSummary = dto.ShowPaymentSummary;
        template.ShowTaxDetails = dto.ShowTaxDetails;
        template.ShowItemSku = dto.ShowItemSku;
        template.ShowDiscountDetails = dto.ShowDiscountDetails;
        template.ShowPrintedBy = dto.ShowPrintedBy;
        template.ShowReceiptNumberBox = dto.ShowReceiptNumberBox;
        template.Copies = Math.Clamp(dto.Copies, 1, 5);
        template.AutoPrintAfterSale = dto.AutoPrintAfterSale;
    }

    private static string NormalizeMode(string? value)
    {
        var mode = (value ?? "thermal").Trim().ToLowerInvariant();
        return mode is "a4" or "80mm" or "58mm" or "thermal" ? mode : "thermal";
    }

    private static ReceiptTemplateDto Map(ReceiptTemplate t) => new()
    {
        Id = t.Id,
        StoreId = t.StoreId,
        StoreName = t.Store is null ? null : $"{t.Store.Code} · {t.Store.Name}",
        Name = t.Name,
        IsDefault = t.IsDefault,
        IsActive = t.IsActive,
        PaperSize = t.PaperSize,
        BusinessName = t.BusinessName,
        Subtitle = t.Subtitle,
        LogoUrl = t.LogoUrl,
        HeaderText = t.HeaderText,
        FooterText = t.FooterText,
        ReturnPolicyText = t.ReturnPolicyText,
        ShowStoreName = t.ShowStoreName,
        ShowCustomer = t.ShowCustomer,
        ShowCashier = t.ShowCashier,
        ShowPaymentSummary = t.ShowPaymentSummary,
        ShowTaxDetails = t.ShowTaxDetails,
        ShowItemSku = t.ShowItemSku,
        ShowDiscountDetails = t.ShowDiscountDetails,
        ShowPrintedBy = t.ShowPrintedBy,
        ShowReceiptNumberBox = t.ShowReceiptNumberBox,
        Copies = t.Copies,
        AutoPrintAfterSale = t.AutoPrintAfterSale
    };

    private static PrintStationSettingDto Map(PrintStationSetting s) => new()
    {
        Id = s.Id,
        StoreId = s.StoreId,
        StoreName = s.Store is null ? null : $"{s.Store.Code} · {s.Store.Name}",
        RegisterId = s.RegisterId,
        RegisterName = s.Register?.Name,
        StationName = s.StationName,
        ReceiptTemplateId = s.ReceiptTemplateId,
        DefaultReceiptMode = s.DefaultReceiptMode,
        UseBrowserPrint = s.UseBrowserPrint,
        AutoPrintReceipt = s.AutoPrintReceipt,
        ReceiptCopies = s.ReceiptCopies,
        ReceiptPrinterName = s.ReceiptPrinterName,
        LabelPrinterName = s.LabelPrinterName,
        UseLocalPrintAgent = s.UseLocalPrintAgent,
        LocalPrintAgentUrl = s.LocalPrintAgentUrl,
        EnableCashDrawerKick = s.EnableCashDrawerKick,
        CashDrawerCommandHex = s.CashDrawerCommandHex
    };

    private static ReceiptTemplateDto DefaultTemplate(Guid? storeId) => new()
    {
        StoreId = storeId,
        Name = "Default Receipt",
        IsDefault = true,
        IsActive = true,
        PaperSize = "80mm",
        Subtitle = "Retail POS Receipt",
        HeaderText = "",
        FooterText = "Thank you for shopping with us.",
        ReturnPolicyText = "Goods sold are subject to the store return policy. Please keep this receipt for exchange, return, or warranty reference.",
        ShowStoreName = true,
        ShowCustomer = true,
        ShowCashier = true,
        ShowPaymentSummary = true,
        ShowTaxDetails = true,
        ShowItemSku = true,
        ShowDiscountDetails = true,
        ShowPrintedBy = true,
        ShowReceiptNumberBox = true,
        Copies = 1
    };

    private static PrintStationSettingDto DefaultStation(Guid storeId, Guid? registerId) => new()
    {
        StoreId = storeId,
        RegisterId = registerId,
        StationName = "Default POS Station",
        DefaultReceiptMode = "thermal",
        UseBrowserPrint = true,
        AutoPrintReceipt = false,
        ReceiptCopies = 1,
        UseLocalPrintAgent = false,
        LocalPrintAgentUrl = "http://localhost:3859",
        EnableCashDrawerKick = false,
        CashDrawerCommandHex = "1B700019FA"
    };
}
