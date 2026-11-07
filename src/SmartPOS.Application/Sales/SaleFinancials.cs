using SmartPOS.Domain.Entities;

namespace SmartPOS.Application.Sales;

public static class SaleFinancials
{
    public static IReadOnlyDictionary<Guid, RefundableLine> GetRefundableLines(Sale sale)
    {
        var lines = sale.Lines.OrderBy(l => l.LineNumber).ThenBy(l => l.Id).ToList();
        var amounts = Allocate(Math.Max(0m, sale.TotalAmount), lines.Select(l => Math.Max(0m, l.LineTotal)).ToList());
        return lines.Select((line, index) => new RefundableLine(
                line.Id,
                line.Quantity,
                amounts[index],
                line.LineTotal <= 0 ? 0m : Math.Min(amounts[index], Math.Max(0m, line.TaxAmount))))
            .ToDictionary(l => l.SaleLineId);
    }

    public static IReadOnlyList<decimal> Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        if (total < 0 || weights.Any(w => w < 0))
            throw new ArgumentOutOfRangeException(nameof(total));

        var result = new decimal[weights.Count];
        var weightTotal = weights.Sum();
        if (weights.Count == 0 || weightTotal == 0 || total == 0)
            return result;

        var cents = Math.Round(total, 2) * 100m;
        var raw = weights.Select(w => cents * w / weightTotal).ToArray();
        var allocated = raw.Select(Math.Floor).ToArray();
        var remaining = (int)(cents - allocated.Sum());
        foreach (var index in Enumerable.Range(0, weights.Count)
                     .OrderByDescending(i => raw[i] - allocated[i]).ThenBy(i => i).Take(remaining))
            allocated[index]++;

        for (var index = 0; index < result.Length; index++)
            result[index] = allocated[index] / 100m;
        return result;
    }

    public static RefundAmounts CalculateRefund(RefundableLine line, decimal returnedQuantity,
        decimal refundedTotal, decimal refundedTax, decimal quantity)
    {
        if (line.Quantity <= 0 || quantity <= 0 || returnedQuantity < 0 || returnedQuantity + quantity > line.Quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var cumulativeQuantity = returnedQuantity + quantity;
        var targetTotal = Math.Round(line.Total * cumulativeQuantity / line.Quantity, 2);
        var targetTax = Math.Round(line.Tax * cumulativeQuantity / line.Quantity, 2);
        var total = Math.Max(0m, Math.Min(line.Total - refundedTotal, targetTotal - refundedTotal));
        var tax = Math.Max(0m, Math.Min(total, targetTax - refundedTax));
        return new RefundAmounts(total - tax, tax, total);
    }

    public static bool HasDuplicateReturnLines(IEnumerable<Guid> lineIds) =>
        lineIds.GroupBy(id => id).Any(group => group.Count() > 1);
}

public sealed record RefundableLine(Guid SaleLineId, decimal Quantity, decimal Total, decimal Tax)
{
    public decimal UnitPrice => Quantity <= 0 ? 0m : Math.Round((Total - Tax) / Quantity, 4);
}

public sealed record RefundAmounts(decimal Subtotal, decimal Tax, decimal Total);
