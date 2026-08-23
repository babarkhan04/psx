using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public bool OverLimit => CreditLimit > 0 && Balance > CreditLimit;
}

public sealed class CustomerDto
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [Range(0, 99999999)]
    public decimal CreditLimit { get; set; }

    /// <summary>Set only on create — seeds an opening balance (existing udhaar).</summary>
    [Range(0, 99999999)]
    public decimal OpeningBalance { get; set; }

    public decimal Balance { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CustomerLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public decimal AvailableCredit => Math.Max(0, CreditLimit - Balance);
    public string Display => string.IsNullOrWhiteSpace(Phone) ? Name : $"{Name} · {Phone}";
}

public sealed class CustomerLedgerEntryDto
{
    public DateTime EntryDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReceivePaymentDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Range(0.01, 99999999, ErrorMessage = "Enter an amount greater than zero.")]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
