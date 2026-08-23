using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

/// <summary>Immutable record of every change to a customer's credit balance.</summary>
public class CustomerLedger : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public CustomerLedgerEntryType EntryType { get; set; }

    /// <summary>Signed amount: positive increases balance (charge), negative reduces it (payment).</summary>
    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime EntryDate { get; set; }

    public Customer? Customer { get; set; }
}
