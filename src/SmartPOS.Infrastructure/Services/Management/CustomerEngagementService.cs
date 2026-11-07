using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Application.Common.Options;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Customers;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class CustomerEngagementService(AppDbContext db, DbContextGate gate, IUserContextAccessor user,
    ITenantContext tenant, IStoreAccessService stores, IDateTimeProvider clock, IOptions<CustomerEngagementOptions> settings,
    IPosService pos) : ICustomerEngagementService
{
    private CustomerEngagementOptions Options => settings.Value;
    private DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(Options.TimeZoneId)).Date;

    private async Task EnsureAsync(bool manage = false, bool birthdays = false, bool sending = false, CancellationToken ct = default)
    {
        var principal = user.Principal;
        if (!CustomerContactRules.CanRead(principal) || (manage && !CustomerContactRules.CanManage(principal)) ||
            (birthdays && !CustomerContactRules.CanViewBirthdays(principal)) || (sending && !CustomerContactRules.CanSendBirthdays(principal)) ||
            tenant.TenantId is not { } tenantId)
            throw new UnauthorizedAccessException("You do not have permission to access this customer feature.");
        var userId = principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!await db.Users.AnyAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive, ct) ||
            !await db.Tenants.AnyAsync(item => item.Id == tenantId && item.IsActive, ct))
            throw new UnauthorizedAccessException("This account or business is inactive.");
    }

    public Task<CustomerEngagementContextDto> GetContextAsync(CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(ct: ct);
        return new CustomerEngagementContextDto
        {
            BusinessDate = Today, TimeZoneId = Options.TimeZoneId,
            BusinessName = await db.Tenants.Where(item => item.Id == tenant.TenantId).Select(item => item.Name).SingleAsync(ct),
            CanManage = CustomerContactRules.CanManage(user.Principal),
            CanViewBirthdays = CustomerContactRules.CanViewBirthdays(user.Principal),
            CanSendBirthdayEmails = CustomerContactRules.CanSendBirthdays(user.Principal),
            EmailConfigured = Options.EmailConfigured
        };
    }, ct);

    private CustomerProfileDto Map(Customer c) => new()
    {
        Id = c.Id, Name = c.Name, Phone = c.Phone ?? string.Empty, Email = c.Email, WhatsAppNumber = c.WhatsAppNumber,
        DateOfBirth = c.DateOfBirth, BirthdayEmailOptIn = c.BirthdayEmailOptIn, Balance = c.Balance, CreditLimit = c.CreditLimit,
        IsBirthdayToday = c.DateOfBirth is { } dob && CustomerContactRules.BirthdayInYear(dob, Today.Year) == Today
    };

    public Task<IReadOnlyList<CustomerProfileDto>> FindAsync(string term, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(ct: ct);
        if (string.IsNullOrWhiteSpace(term)) return (IReadOnlyList<CustomerProfileDto>)Array.Empty<CustomerProfileDto>();
        var text = term.Trim();
        var normalized = CustomerContactRules.NormalizePhone(text, Options.CountryCallingCode);
        var lower = text.ToLowerInvariant();
        var matches = await db.Customers.AsNoTracking().Where(c => c.IsActive &&
            (c.Name.ToLower().Contains(lower) || (c.Phone != null && c.Phone.Contains(text)) ||
             (normalized != null && c.NormalizedPhone == normalized)))
            .OrderBy(c => c.Name).Take(30).ToListAsync(ct);
        return (IReadOnlyList<CustomerProfileDto>)matches.Select(Map).ToList();
    }, ct);

    public Task<CustomerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(ct: ct);
        var c = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
        return c is null ? null : Map(c);
    }, ct);

    public Task<Result<Guid>> SaveProfileAsync(CustomerProfileDto profile, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(manage: true, ct: ct);
        profile.Name = profile.Name?.Trim() ?? string.Empty;
        profile.Phone = profile.Phone?.Trim() ?? string.Empty;
        profile.Email = string.IsNullOrWhiteSpace(profile.Email) ? null : profile.Email.Trim();
        profile.WhatsAppNumber = string.IsNullOrWhiteSpace(profile.WhatsAppNumber) ? null : profile.WhatsAppNumber.Trim();
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(profile, new ValidationContext(profile), errors, true))
            return Result<Guid>.Failure(errors[0].ErrorMessage ?? "Check the customer details.", "VALIDATION");
        var normalized = CustomerContactRules.NormalizePhone(profile.Phone, Options.CountryCallingCode);
        if (normalized is null) return Result<Guid>.Failure("Enter a valid mobile number with its country code or local prefix.", "INVALID_PHONE");
        if (profile.WhatsAppNumber is not null && CustomerContactRules.NormalizePhone(profile.WhatsAppNumber, Options.CountryCallingCode) is null)
            return Result<Guid>.Failure("Enter a valid WhatsApp number.", "INVALID_WHATSAPP");
        if (profile.DateOfBirth?.Date > Today) return Result<Guid>.Failure("Date of birth cannot be in the future.", "INVALID_BIRTHDAY");
        if (profile.BirthdayEmailOptIn && (!ValidEmail(profile.Email) || profile.DateOfBirth is null))
            return Result<Guid>.Failure("An email address and date of birth are needed for birthday emails.", "INVALID_CONSENT");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!profile.AllowSharedPhone && await db.Customers.AnyAsync(c => c.Id != profile.Id && c.NormalizedPhone == normalized, ct))
                return Result<Guid>.Failure("This phone number already belongs to a customer. Search and select that profile, or confirm that a different person shares this number.", "DUPLICATE_PHONE");
            Customer? customer = profile.Id == Guid.Empty ? new Customer { IsActive = true } : await db.Customers.SingleOrDefaultAsync(c => c.Id == profile.Id && c.IsActive, ct);
            if (customer is null) return Result<Guid>.Failure("Customer not found or inactive.", "NOT_FOUND");
            if (profile.Id == Guid.Empty) db.Customers.Add(customer);
            customer.Name = profile.Name;
            customer.Phone = profile.Phone;
            customer.NormalizedPhone = normalized;
            customer.Email = profile.Email;
            customer.WhatsAppNumber = profile.WhatsAppNumber;
            customer.DateOfBirth = profile.DateOfBirth?.Date;
            customer.BirthdayEmailOptIn = profile.BirthdayEmailOptIn;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Guid>.Success(customer.Id);
        });
    }, ct);

    public Task<CustomerHistoryDto> GetHistoryAsync(Guid customerId, int page = 0, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(ct: ct);
        if (!user.Principal!.HasClaim(Permissions.ClaimType, Permissions.Sales.View)) throw new UnauthorizedAccessException("Sales-view permission is required.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.IsActive, ct)) return new CustomerHistoryDto();
        var allowed = await stores.GetAllowedStoreIdsAsync(ct);
        var query = db.Sales.AsNoTracking().Where(s => s.CustomerId == customerId && allowed.Contains(s.StoreId));
        var totals = await query.Select(s => new { s.Id, s.TotalAmount, s.Status }).ToListAsync(ct);
        var refunds = await db.SaleReturns.AsNoTracking().Where(s => s.CustomerId == customerId && allowed.Contains(s.StoreId) && s.Status == SaleReturnStatus.Completed)
            .Select(s => new { s.OriginalSaleId, s.TotalAmount }).ToListAsync(ct);
        page = Math.Clamp(page, 0, Math.Max(0, (totals.Count - 1) / 20));
        var rows = await query.Include(s => s.Payments).OrderByDescending(s => s.CompletedOn).ThenByDescending(s => s.Id).Skip(page * 20).Take(20).ToListAsync(ct);
        var storeNames = await db.Stores.AsNoTracking().Where(s => allowed.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return new CustomerHistoryDto
        {
            TotalCount = totals.Count, Page = page,
            NetSpend = totals.Where(s => s.Status != SaleStatus.Voided).Sum(s => s.TotalAmount) - refunds.Sum(s => s.TotalAmount),
            Purchases = rows.Select(s => new CustomerPurchaseDto
            {
                SaleId = s.Id, SaleNumber = s.SaleNumber, CompletedOn = s.CompletedOn, StoreName = storeNames.GetValueOrDefault(s.StoreId, "Store"),
                Status = s.Status.ToString(), PaymentMethods = string.Join(" / ", s.Payments.Select(p => p.Method.ToString()).Distinct()),
                Total = s.TotalAmount, Refunded = refunds.Where(r => r.OriginalSaleId == s.Id).Sum(r => r.TotalAmount)
            }).ToList()
        };
    }, ct);

    public async Task<PosReceiptDto?> GetPurchaseReceiptAsync(Guid customerId, Guid saleId, CancellationToken ct = default)
    {
        var accessible = await gate.ExecuteAsync(async () =>
        {
            await EnsureAsync(ct: ct);
            if (!user.Principal!.HasClaim(Permissions.ClaimType, Permissions.Sales.View)) throw new UnauthorizedAccessException("Sales-view permission is required.");
            var allowed = await stores.GetAllowedStoreIdsAsync(ct);
            return await db.Sales.AnyAsync(s => s.Id == saleId && s.CustomerId == customerId && allowed.Contains(s.StoreId), ct);
        }, ct);
        return accessible ? await pos.GetReceiptAsync(saleId, ct) : null;
    }

    public Task<IReadOnlyList<CustomerBirthdayDto>> GetBirthdaysAsync(int upcomingDays = 7, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(birthdays: true, ct: ct);
        var today = Today;
        var customers = await db.Customers.AsNoTracking().Where(c => c.IsActive && c.DateOfBirth != null).ToListAsync(ct);
        var mail = await db.CustomerBirthdayEmails.AsNoTracking().Where(m => m.BirthdayYear == today.Year || m.BirthdayYear == today.Year + 1).ToListAsync(ct);
        return (IReadOnlyList<CustomerBirthdayDto>)customers.Select(c =>
        {
            var date = CustomerContactRules.NextBirthday(c.DateOfBirth!.Value, today);
            var sent = mail.SingleOrDefault(m => m.CustomerId == c.Id && m.BirthdayYear == date.Year);
            return new CustomerBirthdayDto { CustomerId = c.Id, Name = c.Name, Phone = c.Phone, Email = c.Email, Birthday = date,
                IsToday = date == today, OptedIn = c.BirthdayEmailOptIn, EmailStatus = sent?.Status ?? "Not queued", LastError = sent?.LastError };
        }).Where(c => c.Birthday <= today.AddDays(Math.Clamp(upcomingDays, 0, 31))).OrderBy(c => c.Birthday).ThenBy(c => c.Name).ToList();
    }, ct);

    public Task<BirthdayWishDto> GetBirthdayWishAsync(Guid customerId, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(sending: true, ct: ct);
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId && c.IsActive, ct);
        var business = await db.Tenants.Where(t => t.Id == tenant.TenantId).Select(t => t.Name).SingleAsync(ct);
        return new BirthdayWishDto { CustomerId = customerId,
            Subject = Render(Options.BirthdaySubject, customer.Name, business), Body = Render(Options.BirthdayMessage, customer.Name, business) };
    }, ct);

    public Task<Result> QueueBirthdayWishAsync(BirthdayWishDto wish, CancellationToken ct = default) => gate.ExecuteAsync(async () =>
    {
        await EnsureAsync(sending: true, ct: ct);
        if (!Options.EmailConfigured) return Result.Failure("Birthday email is not configured. Ask the administrator to configure CustomerEngagement:Smtp.", "EMAIL_NOT_CONFIGURED");
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(wish, new ValidationContext(wish), validation, true) || wish.Subject.Contains('\r') || wish.Subject.Contains('\n'))
            return Result.Failure("Enter a subject (up to 200 characters) and message (up to 5000 characters).", "VALIDATION");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == wish.CustomerId && c.IsActive, ct);
            if (customer?.DateOfBirth is not { } dob || CustomerContactRules.BirthdayInYear(dob, Today.Year) != Today || !customer.BirthdayEmailOptIn || !ValidEmail(customer.Email))
                return Result.Failure("Birthday wishes can be queued today for active customers who opted in and have a valid email and birthday.", "NOT_ELIGIBLE");
            var row = await db.CustomerBirthdayEmails.SingleOrDefaultAsync(m => m.CustomerId == customer.Id && m.BirthdayYear == Today.Year, ct);
            if (row is not null && (row.Status is "Sent" or "Queued" or "Sending" || !wish.ConfirmRetry))
                return Result.Failure(row.Status is "Sent" or "Queued" or "Sending" ? "This year's birthday wish has already been sent or queued." : "Check delivery status with your provider and confirm retry before queueing again.", "DUPLICATE_WISH");
            if (row is null)
            {
                row = new CustomerBirthdayEmail { CustomerId = customer.Id, BirthdayYear = Today.Year };
                db.CustomerBirthdayEmails.Add(row);
            }
            row.Recipient = customer.Email!.Trim(); row.Subject = wish.Subject.Trim(); row.Body = wish.Body.Trim();
            row.Status = "Queued"; row.LastError = null; row.LeaseUntil = null;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }, ct);

    internal static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email) && !email.Contains('\r') && !email.Contains('\n') &&
        MailAddress.TryCreate(email, out var address) && address.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
    internal static string Render(string template, string name, string business) => template.Replace("{CustomerName}", name).Replace("{BusinessName}", business);
}
