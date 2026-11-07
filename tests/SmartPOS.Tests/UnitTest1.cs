using SmartPOS.Application.Sales;
using SmartPOS.Domain.Entities;

namespace SmartPOS.Tests;

public class CombinedDiscountRefundTests
{
    [Fact]
    public void Returning_all_lines_preserves_the_paid_value_after_line_and_invoice_discounts()
    {
        var first = new SaleLine { LineNumber = 1, Quantity = 2, UnitPrice = 500, DiscountAmount = 50, TaxAmount = 95, LineTotal = 1045 };
        var second = new SaleLine { LineNumber = 2, Quantity = 1, UnitPrice = 300, LineTotal = 300 };
        var sale = new Sale { TotalAmount = 1245, DiscountAmount = 150, TaxAmount = 95, Lines = [first, second] };
        var lines = SaleFinancials.GetRefundableLines(sale);
        var refunds = lines.Values.Select(line => SaleFinancials.CalculateRefund(line, 0, 0, 0, line.Quantity)).ToList();
        Assert.Equal(1245m, refunds.Sum(refund => refund.Total));
        Assert.Equal(95m, refunds.Sum(refund => refund.Tax));
    }
}
