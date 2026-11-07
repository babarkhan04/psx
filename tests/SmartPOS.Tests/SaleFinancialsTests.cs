using SmartPOS.Application.Sales;
using SmartPOS.Domain.Entities;

namespace SmartPOS.Tests;

public sealed class SaleFinancialsTests
{
    [Fact]
    public void Refund_allocation_accounts_for_invoice_discounts()
    {
        var line = new SaleLine { Quantity = 1, UnitPrice = 1000, LineTotal = 1000 };
        var sale = new Sale { TotalAmount = 900, DiscountAmount = 100, Lines = [line] };
        var allocation = SaleFinancials.GetRefundableLines(sale)[line.Id];
        Assert.Equal(900m, SaleFinancials.CalculateRefund(allocation, 0, 0, 0, 1).Total);
    }

    [Fact]
    public void Partial_returns_preserve_the_last_cent()
    {
        var allocation = new RefundableLine(Guid.NewGuid(), 3, 10, 1);
        var first = SaleFinancials.CalculateRefund(allocation, 0, 0, 0, 1);
        var second = SaleFinancials.CalculateRefund(allocation, 1, first.Total, first.Tax, 1);
        var third = SaleFinancials.CalculateRefund(allocation, 2, first.Total + second.Total, first.Tax + second.Tax, 1);
        Assert.Equal(10m, first.Total + second.Total + third.Total);
        Assert.Equal(1m, first.Tax + second.Tax + third.Tax);
    }

    [Fact]
    public void Allocation_never_loses_or_creates_money()
    {
        for (var cents = 0; cents < 100; cents++)
        {
            var amounts = SaleFinancials.Allocate(cents / 100m, [1m, 2m, 3m, 5m, 8m]);
            Assert.Equal(cents / 100m, amounts.Sum());
            Assert.All(amounts, amount => Assert.True(amount >= 0 && Math.Round(amount, 2) == amount));
        }
    }

    [Fact]
    public void Zero_value_returns_do_not_create_a_refund()
    {
        var allocation = new RefundableLine(Guid.NewGuid(), 1, 0, 0);
        Assert.Equal(0, SaleFinancials.CalculateRefund(allocation, 0, 0, 0, 1).Total);
    }

    [Fact]
    public void Historic_over_refunds_cannot_create_another_payment()
    {
        var allocation = new RefundableLine(Guid.NewGuid(), 2, 100, 0);
        Assert.Equal(0, SaleFinancials.CalculateRefund(allocation, 1, 110, 0, 1).Total);
    }

    [Fact]
    public void Quantities_over_the_sold_amount_are_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaleFinancials.CalculateRefund(new RefundableLine(Guid.NewGuid(), 1, 100, 0), 0, 0, 0, 2));
}
