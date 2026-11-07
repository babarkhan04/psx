using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;
namespace SmartPOS.Application.Common.Interfaces;

public interface ICustomerEngagementService
{
    Task<CustomerEngagementContextDto> GetContextAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CustomerProfileDto>> FindAsync(string term, CancellationToken ct = default);
    Task<CustomerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default);
    Task<Result<Guid>> SaveProfileAsync(CustomerProfileDto profile, CancellationToken ct = default);
    Task<CustomerHistoryDto> GetHistoryAsync(Guid customerId, int page = 0, CancellationToken ct = default);
    Task<PosReceiptDto?> GetPurchaseReceiptAsync(Guid customerId, Guid saleId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerBirthdayDto>> GetBirthdaysAsync(int upcomingDays = 7, CancellationToken ct = default);
    Task<BirthdayWishDto> GetBirthdayWishAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> QueueBirthdayWishAsync(BirthdayWishDto wish, CancellationToken ct = default);
}
public interface ICustomerEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default);
}
