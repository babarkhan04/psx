using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class PosShortcutService(
    AppDbContext db,
    ITenantContext tenantContext,
    DbContextGate gate) : IPosShortcutService
{
    private static readonly IReadOnlyList<PosShortcutSettingDto> DefaultShortcuts =
    [
        Shortcut("ShowHelp", "Show shortcut help", "ShowShortcutHelp", "F1", false, false, false, true, false, 10),
        Shortcut("FocusScan", "Focus barcode / SKU scan", "FocusSearch", "F2", false, false, false, true, false, 20),
        Shortcut("Customer", "Select customer", "TriggerCustomerSelect", "F4", false, false, false, true, false, 30),
        Shortcut("Payment", "Open split payment", "TriggerPaymentMenu", "F8", false, false, false, true, false, 40),
        Shortcut("Reprint", "Reprint last receipt", "TriggerReprintLastReceipt", "F9", false, false, false, true, false, 50),
        Shortcut("Cash", "Cash payment", "TriggerCashPay", "F10", false, false, false, true, false, 60),
        Shortcut("Hold", "Hold current sale", "TriggerHoldSale", "H", false, true, false, true, false, 70),
        Shortcut("RetrieveHeld", "Retrieve held sale", "TriggerRetrieveHeldSale", "G", false, true, false, true, false, 80),
        Shortcut("InvoiceDiscount", "Invoice discount", "TriggerInvoiceDiscount", "D", false, true, false, true, false, 90),
        Shortcut("QuickQuantity", "Quick quantity", "TriggerQuickQuantity", "Q", false, true, false, true, false, 100),
        Shortcut("FullScreenLock", "Toggle POS full-screen lock", "TriggerFullScreenLock", "L", false, true, false, true, false, 105),
        Shortcut("ClearCart", "Clear cart", "TriggerClearCart", "C", false, true, false, true, false, 110),
        Shortcut("CustomerDisplay", "Open customer display", "TriggerCustomerDisplay", "J", false, true, false, true, false, 120),
        Shortcut("ShiftMenu", "Open shift menu", "TriggerShiftMenu", "S", false, true, false, true, false, 130),
        Shortcut("XReport", "Open X report", "TriggerXReport", "X", false, true, false, true, false, 140),
        Shortcut("ZReport", "Open Z report / shifts", "TriggerZReport", "Z", false, true, false, true, false, 150),
        Shortcut("RemoveSelected", "Remove selected cart item", "TriggerRemoveSelectedLine", "Delete", false, false, false, false, true, 160),
        Shortcut("IncreaseQty", "Increase selected quantity", "TriggerIncreaseSelectedLine", "+", false, false, false, false, true, 170),
        Shortcut("DecreaseQty", "Decrease selected quantity", "TriggerDecreaseSelectedLine", "-", false, false, false, false, true, 180),
    ];

    public Task<IReadOnlyList<PosShortcutSettingDto>> GetAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetCoreAsync(ct), ct);

    public Task<Result> SaveAsync(IReadOnlyList<UpdatePosShortcutDto> shortcuts, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SaveCoreAsync(shortcuts, ct), ct);

    public Task<Result> ResetDefaultsAsync(CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ResetDefaultsCoreAsync(ct), ct);

    private async Task<IReadOnlyList<PosShortcutSettingDto>> GetCoreAsync(CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return DefaultShortcuts;
        }

        var saved = await db.PosShortcutSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        return MergeWithDefaults(saved);
    }

    private async Task<Result> SaveCoreAsync(IReadOnlyList<UpdatePosShortcutDto> shortcuts, CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure("Tenant context is not available.");
        }

        var merged = MergeWithDefaults([])
            .Select(d =>
            {
                var update = shortcuts.FirstOrDefault(s => string.Equals(s.ActionCode, d.ActionCode, StringComparison.OrdinalIgnoreCase));
                if (update is null)
                {
                    return d;
                }

                d.Key = NormalizeKey(update.Key);
                d.Ctrl = update.Ctrl;
                d.Alt = update.Alt;
                d.Shift = update.Shift;
                d.IsEnabled = update.IsEnabled;
                d.ShortcutText = BuildShortcutText(d.Key, d.Ctrl, d.Alt, d.Shift);
                return d;
            })
            .ToList();

        var conflict = FindConflict(merged);
        if (conflict is not null)
        {
            return Result.Failure(conflict);
        }

        var existing = await db.PosShortcutSettings
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var shortcut in merged)
        {
            var entity = existing.FirstOrDefault(x => x.ActionCode == shortcut.ActionCode);
            if (entity is null)
            {
                entity = new PosShortcutSetting
                {
                    TenantId = tenantId,
                    ActionCode = shortcut.ActionCode,
                    ActionName = shortcut.ActionName,
                    SortOrder = shortcut.SortOrder
                };
                db.PosShortcutSettings.Add(entity);
            }

            entity.ActionName = shortcut.ActionName;
            entity.Key = shortcut.Key;
            entity.Ctrl = shortcut.Ctrl;
            entity.Alt = shortcut.Alt;
            entity.Shift = shortcut.Shift;
            entity.IsEnabled = shortcut.IsEnabled;
            entity.SortOrder = shortcut.SortOrder;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ResetDefaultsCoreAsync(CancellationToken ct)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure("Tenant context is not available.");
        }

        var existing = await db.PosShortcutSettings
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        db.PosShortcutSettings.RemoveRange(existing);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static IReadOnlyList<PosShortcutSettingDto> MergeWithDefaults(IReadOnlyList<PosShortcutSetting> saved)
    {
        var result = new List<PosShortcutSettingDto>();
        foreach (var d in DefaultShortcuts.OrderBy(x => x.SortOrder))
        {
            var s = saved.FirstOrDefault(x => string.Equals(x.ActionCode, d.ActionCode, StringComparison.OrdinalIgnoreCase));
            result.Add(new PosShortcutSettingDto
            {
                ActionCode = d.ActionCode,
                ActionName = d.ActionName,
                MethodName = d.MethodName,
                Key = NormalizeKey(s?.Key ?? d.Key),
                Ctrl = s?.Ctrl ?? d.Ctrl,
                Alt = s?.Alt ?? d.Alt,
                Shift = s?.Shift ?? d.Shift,
                IsEnabled = s?.IsEnabled ?? d.IsEnabled,
                AllowWhenTyping = d.AllowWhenTyping,
                IsCartShortcut = d.IsCartShortcut,
                SortOrder = d.SortOrder
            });
        }

        foreach (var item in result)
        {
            item.ShortcutText = BuildShortcutText(item.Key, item.Ctrl, item.Alt, item.Shift);
        }

        return result;
    }

    private static string? FindConflict(IReadOnlyList<PosShortcutSettingDto> shortcuts)
    {
        var groups = shortcuts
            .Where(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.Key))
            .GroupBy(s => ShortcutSignature(s.Key, s.Ctrl, s.Alt, s.Shift))
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            return null;
        }

        var group = groups[0].ToList();
        return $"Shortcut conflict: {group[0].ShortcutText} is used by {string.Join(", ", group.Select(x => x.ActionName))}.";
    }

    private static PosShortcutSettingDto Shortcut(
        string code,
        string name,
        string method,
        string key,
        bool ctrl,
        bool alt,
        bool shift,
        bool allowTyping,
        bool isCartShortcut,
        int sortOrder) => new()
        {
            ActionCode = code,
            ActionName = name,
            MethodName = method,
            Key = NormalizeKey(key),
            Ctrl = ctrl,
            Alt = alt,
            Shift = shift,
            IsEnabled = true,
            AllowWhenTyping = allowTyping,
            IsCartShortcut = isCartShortcut,
            SortOrder = sortOrder,
            ShortcutText = BuildShortcutText(key, ctrl, alt, shift)
        };

    private static string NormalizeKey(string? key)
    {
        key = (key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return key.Length == 1 ? key.ToUpperInvariant() : key;
    }

    private static string ShortcutSignature(string key, bool ctrl, bool alt, bool shift) =>
        $"{ctrl}:{alt}:{shift}:{NormalizeKey(key).ToUpperInvariant()}";

    private static string BuildShortcutText(string key, bool ctrl, bool alt, bool shift)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        parts.Add(NormalizeKey(key));
        return string.Join(" + ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
