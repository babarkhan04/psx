using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class RiskService(AppDbContext db, DbContextGate gate) : IRiskService
{
    private const decimal HighDiscountRatePercent = 15m;
    private const decimal MediumDiscountRatePercent = 8m;
    private const decimal HighCashVariance = 1000m;
    private const decimal MediumCashVariance = 250m;

    public Task<RiskDashboardDto> GetRiskDashboardAsync(RiskQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetRiskDashboardCoreAsync(query, ct), ct);

    private async Task<RiskDashboardDto> GetRiskDashboardCoreAsync(RiskQueryDto query, CancellationToken ct)
    {
        var fromUtc = query.FromUtc ?? DateTime.UtcNow.Date.AddDays(-29);
        var toUtc = query.ToUtc ?? DateTime.UtcNow.Date.AddDays(1);

        if (toUtc <= fromUtc)
        {
            toUtc = fromUtc.AddDays(1);
        }

        var stores = await db.Stores.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => FormatStore(s.Code, s.Name), ct);

        var users = await db.Users.AsNoTracking()
            .Select(u => new
            {
                u.Id,
                Name = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? u.UserName ?? u.Id) : u.DisplayName
            })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var salesQuery = db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Where(s => s.CompletedOn >= fromUtc && s.CompletedOn < toUtc);

        var returnsQuery = db.SaleReturns.AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.ProcessedOn >= fromUtc && r.ProcessedOn < toUtc && r.Status == SaleReturnStatus.Completed);

        var shiftsQuery = db.Shifts.AsNoTracking()
            .Where(s => s.OpenedOn < toUtc && (s.ClosedOn == null || s.ClosedOn >= fromUtc));

        var cashQuery = db.CashMovements.AsNoTracking()
            .Where(c => c.OccurredOn >= fromUtc && c.OccurredOn < toUtc);

        var approvalsQuery = db.ManagerApprovalLogs.AsNoTracking()
            .Where(a => a.ApprovedOn >= fromUtc && a.ApprovedOn < toUtc);

        if (query.StoreId is { } storeId)
        {
            salesQuery = salesQuery.Where(s => s.StoreId == storeId);
            returnsQuery = returnsQuery.Where(r => r.StoreId == storeId);
            shiftsQuery = shiftsQuery.Where(s => s.StoreId == storeId);
            cashQuery = cashQuery.Where(c => c.StoreId == storeId);
            approvalsQuery = approvalsQuery.Where(a => a.StoreId == null || a.StoreId == storeId);
        }

        var sales = await salesQuery.ToListAsync(ct);
        var returns = await returnsQuery.ToListAsync(ct);
        var shifts = await shiftsQuery.ToListAsync(ct);
        var cashMovements = await cashQuery.ToListAsync(ct);
        var approvals = await approvalsQuery.ToListAsync(ct);

        var rowsByUser = new Dictionary<string, CashierRiskAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var sale in sales)
        {
            var row = GetRow(rowsByUser, sale.CashierUserId);
            row.StoreIds.Add(sale.StoreId);
            row.LastActivityOn = Max(row.LastActivityOn, sale.CompletedOn);

            if (sale.Status == SaleStatus.Voided)
            {
                row.VoidCount++;
                row.VoidAmount += sale.TotalAmount;
                continue;
            }

            row.TransactionCount++;
            row.NetSales += sale.TotalAmount;
            row.DiscountAmount += sale.DiscountAmount + sale.Lines.Sum(l => l.DiscountAmount);
        }

        foreach (var saleReturn in returns)
        {
            var row = GetRow(rowsByUser, saleReturn.ProcessedByUserId);
            row.StoreIds.Add(saleReturn.StoreId);
            row.ReturnCount++;
            row.ReturnAmount += saleReturn.TotalAmount;
            row.LastActivityOn = Max(row.LastActivityOn, saleReturn.ProcessedOn);
        }

        var shiftsById = shifts.ToDictionary(s => s.Id, s => s);

        foreach (var shift in shifts)
        {
            var responsibleUserId = string.IsNullOrWhiteSpace(shift.ClosedByUserId) ? shift.OpenedByUserId : shift.ClosedByUserId!;
            var row = GetRow(rowsByUser, responsibleUserId);
            row.StoreIds.Add(shift.StoreId);
            row.CashVariance += Math.Abs(shift.Variance);
            row.LastActivityOn = Max(row.LastActivityOn, shift.ClosedOn ?? shift.OpenedOn);
        }

        foreach (var movement in cashMovements)
        {
            if (!shiftsById.TryGetValue(movement.ShiftId, out var shift))
            {
                continue;
            }

            var responsibleUserId = string.IsNullOrWhiteSpace(shift.OpenedByUserId) ? shift.ClosedByUserId ?? string.Empty : shift.OpenedByUserId;
            if (string.IsNullOrWhiteSpace(responsibleUserId))
            {
                continue;
            }

            var row = GetRow(rowsByUser, responsibleUserId);
            row.StoreIds.Add(movement.StoreId);
            row.LastActivityOn = Max(row.LastActivityOn, movement.OccurredOn);

            if (movement.MovementType == CashMovementType.CashOut)
            {
                row.CashOutCount++;
            }
            else if (movement.MovementType == CashMovementType.Adjustment)
            {
                row.CashAdjustmentCount++;
            }
        }

        foreach (var approval in approvals)
        {
            var userId = string.IsNullOrWhiteSpace(approval.RequestedByUserId) ? approval.ApprovedByUserId : approval.RequestedByUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                continue;
            }

            var row = GetRow(rowsByUser, userId);
            if (approval.StoreId is { } approvalStoreId)
            {
                row.StoreIds.Add(approvalStoreId);
            }

            row.ApprovalRequestCount++;
            row.LastActivityOn = Max(row.LastActivityOn, approval.ApprovedOn);

            switch (approval.ApprovalType)
            {
                case ManagerApprovalType.PriceOverride:
                    row.PriceOverrideApprovalCount++;
                    break;
                case ManagerApprovalType.Discount:
                    row.DiscountApprovalCount++;
                    break;
                case ManagerApprovalType.Refund:
                    row.RefundApprovalCount++;
                    break;
            }
        }

        var cashierRows = rowsByUser.Values
            .Select(row => BuildRiskRow(row, users, stores))
            .Where(row => query.RiskLevel.Equals("All", StringComparison.OrdinalIgnoreCase) || row.RiskLevel.Equals(query.RiskLevel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.LastActivityOn)
            .ToList();

        var events = BuildRiskEvents(sales, returns, shifts, approvals, users, stores)
            .OrderByDescending(e => e.EventOn)
            .Take(500)
            .ToList();

        return new RiskDashboardDto
        {
            Summary = new RiskSummaryDto
            {
                CashierCount = cashierRows.Count,
                HighRiskCount = cashierRows.Count(r => r.RiskLevel == "High"),
                MediumRiskCount = cashierRows.Count(r => r.RiskLevel == "Medium"),
                LowRiskCount = cashierRows.Count(r => r.RiskLevel == "Low"),
                TotalDiscounts = cashierRows.Sum(r => r.DiscountAmount),
                TotalVoids = cashierRows.Sum(r => r.VoidAmount),
                TotalReturns = cashierRows.Sum(r => r.ReturnAmount),
                TotalCashVariance = cashierRows.Sum(r => r.CashVariance),
                ApprovalCount = cashierRows.Sum(r => r.ApprovalRequestCount)
            },
            Cashiers = cashierRows,
            Events = events,
            RiskBreakdown = cashierRows.GroupBy(r => r.RiskLevel)
                .Select(g => new RiskBreakdownDto { Name = g.Key, Count = g.Count(), Amount = g.Sum(x => x.NetSales) })
                .OrderBy(x => RiskLevelRank(x.Name))
                .ToList(),
            EventBreakdown = events.GroupBy(e => e.EventType)
                .Select(g => new RiskBreakdownDto { Name = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount ?? 0m) })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    private static CashierRiskRowDto BuildRiskRow(
        CashierRiskAccumulator row,
        IReadOnlyDictionary<string, string> users,
        IReadOnlyDictionary<Guid, string> stores)
    {
        var issues = new List<string>();
        var score = 0;
        var discountRate = row.NetSales <= 0 ? 0 : row.DiscountAmount / row.NetSales * 100m;
        var averageBasket = row.TransactionCount == 0 ? 0 : row.NetSales / row.TransactionCount;

        if (discountRate >= HighDiscountRatePercent)
        {
            score += 25;
            issues.Add($"High discount rate {discountRate:N1}%");
        }
        else if (discountRate >= MediumDiscountRatePercent)
        {
            score += 12;
            issues.Add($"Discount rate {discountRate:N1}%");
        }

        if (row.VoidCount >= 5)
        {
            score += 25;
            issues.Add($"{row.VoidCount} voids");
        }
        else if (row.VoidCount > 0)
        {
            score += Math.Min(15, row.VoidCount * 5);
            issues.Add($"{row.VoidCount} void(s)");
        }

        if (row.ReturnCount >= 5)
        {
            score += 20;
            issues.Add($"{row.ReturnCount} returns");
        }
        else if (row.ReturnCount > 0)
        {
            score += Math.Min(12, row.ReturnCount * 3);
            issues.Add($"{row.ReturnCount} return(s)");
        }

        if (row.CashVariance >= HighCashVariance)
        {
            score += 25;
            issues.Add($"Cash variance {row.CashVariance:N0}");
        }
        else if (row.CashVariance >= MediumCashVariance)
        {
            score += 12;
            issues.Add($"Cash variance {row.CashVariance:N0}");
        }

        if (row.PriceOverrideApprovalCount > 0)
        {
            score += Math.Min(15, row.PriceOverrideApprovalCount * 5);
            issues.Add($"{row.PriceOverrideApprovalCount} price override approval(s)");
        }

        if (row.CashOutCount >= 3)
        {
            score += 10;
            issues.Add($"{row.CashOutCount} cash-out entries");
        }

        if (row.CashAdjustmentCount > 0)
        {
            score += Math.Min(12, row.CashAdjustmentCount * 4);
            issues.Add($"{row.CashAdjustmentCount} cash adjustment(s)");
        }

        if (row.RefundApprovalCount > 0)
        {
            score += Math.Min(12, row.RefundApprovalCount * 4);
            issues.Add($"{row.RefundApprovalCount} refund approval(s)");
        }

        score = Math.Clamp(score, 0, 100);
        var level = score >= 70 ? "High" : score >= 35 ? "Medium" : "Low";

        return new CashierRiskRowDto
        {
            CashierUserId = row.UserId,
            CashierName = NameFor(users, row.UserId),
            StoreNames = row.StoreIds.Count == 0 ? "-" : string.Join(", ", row.StoreIds.Select(id => StoreName(stores, id)).Distinct().OrderBy(x => x)),
            TransactionCount = row.TransactionCount,
            NetSales = row.NetSales,
            AverageBasket = averageBasket,
            DiscountAmount = row.DiscountAmount,
            DiscountRatePercent = discountRate,
            VoidCount = row.VoidCount,
            VoidAmount = row.VoidAmount,
            ReturnCount = row.ReturnCount,
            ReturnAmount = row.ReturnAmount,
            CashOutCount = row.CashOutCount,
            CashAdjustmentCount = row.CashAdjustmentCount,
            CashVariance = row.CashVariance,
            ApprovalRequestCount = row.ApprovalRequestCount,
            PriceOverrideApprovalCount = row.PriceOverrideApprovalCount,
            DiscountApprovalCount = row.DiscountApprovalCount,
            RefundApprovalCount = row.RefundApprovalCount,
            RiskScore = score,
            RiskLevel = level,
            KeyIssues = issues.Count == 0 ? "No unusual pattern detected" : string.Join("; ", issues.Take(4)),
            LastActivityOn = row.LastActivityOn
        };
    }

    private static IReadOnlyList<RiskEventDto> BuildRiskEvents(
        IReadOnlyList<Sale> sales,
        IReadOnlyList<SaleReturn> returns,
        IReadOnlyList<Shift> shifts,
        IReadOnlyList<ManagerApprovalLog> approvals,
        IReadOnlyDictionary<string, string> users,
        IReadOnlyDictionary<Guid, string> stores)
    {
        var events = new List<RiskEventDto>();

        foreach (var sale in sales.Where(s => s.Status == SaleStatus.Voided))
        {
            events.Add(new RiskEventDto
            {
                EventOn = sale.CompletedOn,
                StoreName = StoreName(stores, sale.StoreId),
                UserName = NameFor(users, sale.CashierUserId),
                EventType = "Void",
                Reference = sale.SaleNumber,
                Amount = sale.TotalAmount,
                Severity = sale.TotalAmount >= 5000 ? "High" : "Medium",
                Details = string.IsNullOrWhiteSpace(sale.Notes) ? "Sale was voided." : sale.Notes!,
                ActionUrl = "/sales"
            });
        }

        foreach (var sale in sales.Where(s => s.Status != SaleStatus.Voided && (s.DiscountAmount > 0 || s.Lines.Any(l => l.DiscountAmount > 0))))
        {
            var discount = sale.DiscountAmount + sale.Lines.Sum(l => l.DiscountAmount);
            if (discount <= 0)
            {
                continue;
            }

            var rate = sale.TotalAmount <= 0 ? 0 : discount / sale.TotalAmount * 100m;
            events.Add(new RiskEventDto
            {
                EventOn = sale.CompletedOn,
                StoreName = StoreName(stores, sale.StoreId),
                UserName = NameFor(users, sale.CashierUserId),
                EventType = "Discount",
                Reference = sale.SaleNumber,
                Amount = discount,
                Severity = rate >= HighDiscountRatePercent ? "High" : "Medium",
                Details = $"Discount {discount:N2} ({rate:N1}%). Reason: {sale.DiscountReason ?? "-"}",
                ActionUrl = "/sales"
            });
        }

        foreach (var saleReturn in returns)
        {
            events.Add(new RiskEventDto
            {
                EventOn = saleReturn.ProcessedOn,
                StoreName = StoreName(stores, saleReturn.StoreId),
                UserName = NameFor(users, saleReturn.ProcessedByUserId),
                EventType = saleReturn.ReturnType.ToString(),
                Reference = saleReturn.ReturnNumber,
                Amount = saleReturn.TotalAmount,
                Severity = saleReturn.TotalAmount >= 5000 ? "High" : "Medium",
                Details = $"Reason: {saleReturn.ReturnReason}. Method: {saleReturn.RefundMethod}.",
                ActionUrl = "/sales"
            });
        }

        foreach (var shift in shifts.Where(s => Math.Abs(s.Variance) > 0))
        {
            var userId = string.IsNullOrWhiteSpace(shift.ClosedByUserId) ? shift.OpenedByUserId : shift.ClosedByUserId!;
            events.Add(new RiskEventDto
            {
                EventOn = shift.ClosedOn ?? shift.OpenedOn,
                StoreName = StoreName(stores, shift.StoreId),
                UserName = NameFor(users, userId),
                EventType = "Cash variance",
                Reference = shift.ShiftNumber,
                Amount = Math.Abs(shift.Variance),
                Severity = Math.Abs(shift.Variance) >= HighCashVariance ? "High" : "Medium",
                Details = $"Expected {shift.ExpectedCash:N2}, counted {shift.CountedCash:N2}, variance {shift.Variance:N2}.",
                ActionUrl = "/shifts"
            });
        }

        foreach (var approval in approvals)
        {
            events.Add(new RiskEventDto
            {
                EventOn = approval.ApprovedOn,
                StoreName = approval.StoreId is { } storeId ? StoreName(stores, storeId) : "All stores",
                UserName = NameFor(users, string.IsNullOrWhiteSpace(approval.RequestedByUserId) ? approval.ApprovedByUserId : approval.RequestedByUserId),
                EventType = $"Approval: {approval.ApprovalType}",
                Reference = approval.Reference ?? "-",
                Amount = approval.Amount,
                Severity = approval.ApprovalType is ManagerApprovalType.VoidSale or ManagerApprovalType.Refund or ManagerApprovalType.PriceOverride ? "Medium" : "Info",
                Details = $"Approved by {approval.ApprovedByUserName}. Reason: {approval.Reason}",
                ActionUrl = "/audit"
            });
        }

        return events;
    }

    private static CashierRiskAccumulator GetRow(Dictionary<string, CashierRiskAccumulator> rows, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "unknown";
        }

        if (!rows.TryGetValue(userId, out var row))
        {
            row = new CashierRiskAccumulator { UserId = userId };
            rows[userId] = row;
        }

        return row;
    }

    private static DateTime? Max(DateTime? current, DateTime value) => current is null || value > current.Value ? value : current;

    private static string NameFor(IReadOnlyDictionary<string, string> users, string userId) =>
        users.TryGetValue(userId, out var name) ? name : userId;

    private static string StoreName(IReadOnlyDictionary<Guid, string> stores, Guid storeId) =>
        stores.TryGetValue(storeId, out var name) ? name : "Unknown store";

    private static string FormatStore(string code, string name) =>
        string.IsNullOrWhiteSpace(code) ? name : $"{code} · {name}";

    private static int RiskLevelRank(string level) => level switch
    {
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 4
    };

    private sealed class CashierRiskAccumulator
    {
        public string UserId { get; set; } = string.Empty;
        public HashSet<Guid> StoreIds { get; } = [];
        public int TransactionCount { get; set; }
        public decimal NetSales { get; set; }
        public decimal DiscountAmount { get; set; }
        public int VoidCount { get; set; }
        public decimal VoidAmount { get; set; }
        public int ReturnCount { get; set; }
        public decimal ReturnAmount { get; set; }
        public int CashOutCount { get; set; }
        public int CashAdjustmentCount { get; set; }
        public decimal CashVariance { get; set; }
        public int ApprovalRequestCount { get; set; }
        public int PriceOverrideApprovalCount { get; set; }
        public int DiscountApprovalCount { get; set; }
        public int RefundApprovalCount { get; set; }
        public DateTime? LastActivityOn { get; set; }
    }
}
