using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// A customer who can buy on credit (udhaar). <see cref="Balance"/> is the
/// outstanding amount the customer owes; it rises with credit sales and falls
/// when payments are received. Every change is recorded in <see cref="CustomerLedger"/>.
/// </summary>
public class Customer : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? NormalizedPhone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool BirthdayEmailOptIn { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }

    /// <summary>Maximum outstanding credit allowed. 0 means cash-only (no credit).</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Current outstanding balance (udhaar). Positive = customer owes us.</summary>
    public decimal Balance { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CustomerLedger> Ledger { get; set; } = [];
}
