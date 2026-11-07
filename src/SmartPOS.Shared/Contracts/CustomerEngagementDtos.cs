using System.ComponentModel.DataAnnotations;
namespace SmartPOS.Shared.Contracts;

public sealed class CustomerProfileDto
{
    public Guid Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Phone { get; set; } = string.Empty;
    private string? _email;
    [EmailAddress, StringLength(200)] public string? Email { get => _email; set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    [StringLength(40)] public string? WhatsAppNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool BirthdayEmailOptIn { get; set; }
    public bool AllowSharedPhone { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public bool IsBirthdayToday { get; set; }
    public CustomerLookupDto ToLookup() => new() { Id = Id, Name = Name, Phone = Phone, CreditLimit = CreditLimit, Balance = Balance };
}
public sealed class CustomerEngagementContextDto
{
    public DateTime BusinessDate { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string BusinessName { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public bool CanViewBirthdays { get; set; }
    public bool CanSendBirthdayEmails { get; set; }
    public bool EmailConfigured { get; set; }
}
public sealed class CustomerPurchaseDto
{
    public Guid SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethods { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Refunded { get; set; }
}
public sealed class CustomerHistoryDto
{
    public IReadOnlyList<CustomerPurchaseDto> Purchases { get; set; } = [];
    public int TotalCount { get; set; }
    public decimal NetSpend { get; set; }
    public int Page { get; set; }
}
public sealed class PosCustomerSelectionDto
{
    public bool WalkIn { get; set; }
    public CustomerLookupDto? Customer { get; set; }
    public bool IsBirthdayToday { get; set; }
}
public sealed class CustomerBirthdayDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime Birthday { get; set; }
    public bool IsToday { get; set; }
    public bool OptedIn { get; set; }
    public string EmailStatus { get; set; } = "Not queued";
    public string? LastError { get; set; }
}
public sealed class BirthdayWishDto
{
    public Guid CustomerId { get; set; }
    [Required, StringLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(5000)] public string Body { get; set; } = string.Empty;
    public bool ConfirmRetry { get; set; }
}
