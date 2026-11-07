using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Options;
using SmartPOS.Domain.Customers;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Services.Management;

namespace SmartPOS.Infrastructure.Services;

public sealed class CustomerSmtpSender(IOptions<CustomerEngagementOptions> settings) : ICustomerEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
    {
        var options = settings.Value;
        if (!options.EmailConfigured) throw new InvalidOperationException("Customer email delivery is not configured.");
        var smtp = options.Smtp;
        using var message = new MailMessage(new MailAddress(smtp.FromAddress, smtp.FromName), new MailAddress(recipient))
            { Subject = subject, Body = body, IsBodyHtml = false, BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8 };
        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl, UseDefaultCredentials = false };
        if (!string.IsNullOrWhiteSpace(smtp.Username)) client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        await client.SendMailAsync(message, timeout.Token);
    }
}

public sealed class CustomerBirthdayDispatcher(AppDbContext db, ITenantContext tenant, IDateTimeProvider clock,
    IOptions<CustomerEngagementOptions> settings, ICustomerEmailSender sender, ILogger<CustomerBirthdayDispatcher> logger)
{
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        var options = settings.Value;
        if (!options.EmailConfigured || !options.ProcessEmailQueue) return;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId));
        await db.CustomerBirthdayEmails.IgnoreQueryFilters().Where(m => !m.IsDeleted && m.Status == "Sending" && (m.LeaseUntil == null || m.LeaseUntil <= clock.UtcNow))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, "Uncertain").SetProperty(m => m.LeaseUntil, (DateTime?)null)
                .SetProperty(m => m.LastError, "Delivery was interrupted. Check your email provider before confirming a retry."), ct);
        if (options.AutomaticBirthdayEmails && local.Hour >= Math.Clamp(options.AutomaticSendHour, 0, 23))
            await QueueAutomaticAsync(local.Date, options, ct);
        var pending = await db.CustomerBirthdayEmails.IgnoreQueryFilters().AsNoTracking()
            .Where(m => !m.IsDeleted && m.Status == "Queued").OrderBy(m => m.CreatedOn).Take(20).ToListAsync(ct);
        foreach (var candidate in pending)
        {
            ct.ThrowIfCancellationRequested();
            tenant.SetTenant(candidate.TenantId);
            var lease = clock.UtcNow.AddMinutes(10);
            var claimed = await db.CustomerBirthdayEmails.Where(m => m.Id == candidate.Id && m.Status == "Queued")
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, "Sending").SetProperty(m => m.LeaseUntil, (DateTime?)lease)
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
            if (claimed != 1) continue;
            var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == candidate.CustomerId && c.IsActive, ct);
            var activeBusiness = await db.Tenants.AnyAsync(t => t.Id == candidate.TenantId && t.IsActive, ct);
            if (!activeBusiness || customer?.DateOfBirth is not { } birthday || !customer.BirthdayEmailOptIn ||
                !CustomerEngagementService.ValidEmail(customer.Email) ||
                !string.Equals(customer.Email?.Trim(), candidate.Recipient, StringComparison.OrdinalIgnoreCase) ||
                candidate.BirthdayYear != local.Year || CustomerContactRules.BirthdayInYear(birthday, local.Year) != local.Date)
            {
                await FinishAsync(candidate.Id, lease, "Cancelled", "Eligibility or contact details changed before delivery.", ct);
                continue;
            }
            try
            {
                await sender.SendAsync(candidate.Recipient, candidate.Subject, candidate.Body, ct);
                await FinishAsync(candidate.Id, lease, "Sent", null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Birthday email delivery could not be confirmed for outbox item {EmailId}", candidate.Id);
                await FinishAsync(candidate.Id, lease, "Uncertain", "Delivery could not be confirmed. Check your email provider before confirming a retry.", ct);
            }
        }
    }

    private Task<int> FinishAsync(Guid id, DateTime lease, string status, string? error, CancellationToken ct) =>
        db.CustomerBirthdayEmails.Where(m => m.Id == id && m.Status == "Sending" && m.LeaseUntil == lease)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, status).SetProperty(m => m.LastError, error)
                .SetProperty(m => m.SentOn, status == "Sent" ? (DateTime?)clock.UtcNow : null).SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);

    private async Task QueueAutomaticAsync(DateTime today, CustomerEngagementOptions options, CancellationToken ct)
    {
        var businesses = await db.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        foreach (var business in businesses)
        {
            tenant.SetTenant(business.Id);
            var customers = await db.Customers.AsNoTracking().Where(c => c.IsActive && c.BirthdayEmailOptIn && c.DateOfBirth != null && c.Email != null).ToListAsync(ct);
            foreach (var c in customers.Where(c => CustomerContactRules.BirthdayInYear(c.DateOfBirth!.Value, today.Year) == today && CustomerEngagementService.ValidEmail(c.Email)))
            {
                if (await db.CustomerBirthdayEmails.AnyAsync(m => m.CustomerId == c.Id && m.BirthdayYear == today.Year, ct)) continue;
                var subject = CustomerEngagementService.Render(options.BirthdaySubject, c.Name, business.Name);
                var body = CustomerEngagementService.Render(options.BirthdayMessage, c.Name, business.Name);
                if (subject.Length > 200 || body.Length > 5000 || subject.Contains('\r') || subject.Contains('\n'))
                {
                    logger.LogWarning("Birthday email template exceeds limits for business {TenantId}", business.Id);
                    continue;
                }
                db.CustomerBirthdayEmails.Add(new CustomerBirthdayEmail
                {
                    TenantId = business.Id, CustomerId = c.Id, BirthdayYear = today.Year,
                    Recipient = c.Email!.Trim(), Subject = subject, Body = body, Status = "Queued"
                });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    if (!await db.CustomerBirthdayEmails.AnyAsync(m => m.CustomerId == c.Id && m.BirthdayYear == today.Year, ct)) throw;
                }
            }
        }
    }
}

public sealed class CustomerBirthdayWorker(IServiceScopeFactory scopes, ILogger<CustomerBirthdayWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CustomerBirthdayDispatcher>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Customer birthday email processing failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
