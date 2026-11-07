using SmartPOS.Domain.Common;
namespace SmartPOS.Domain.Entities;

public class CustomerBirthdayEmail : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public int BirthdayYear { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public int Attempts { get; set; }
    public DateTime? SentOn { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastError { get; set; }
    public Customer? Customer { get; set; }
}
